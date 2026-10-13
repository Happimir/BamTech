using MediatR;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Business.Data;
using StargateAPI.Business.Domain;
using StargateAPI.Business.Dtos;
using StargateAPI.Business.Results;

namespace StargateAPI.Business.Commands
{
    /// <summary>
    /// Replaces a person's name fields by stable ID. Version is required: a missing version is a
    /// 400 and a stale one is a 409. Identity and duty history are unaffected by a rename.
    /// </summary>
    public sealed record UpdatePerson(
        Guid Id,
        string? Name,
        string? FirstName,
        string? MiddleName,
        string? LastName,
        Guid? Version) : IRequest<Result<PersonDetailDto>>;

    public class UpdatePersonHandler : IRequestHandler<UpdatePerson, Result<PersonDetailDto>>
    {
        private readonly StargateContext _context;

        public UpdatePersonHandler(StargateContext context)
        {
            _context = context;
        }

        public async Task<Result<PersonDetailDto>> Handle(UpdatePerson request, CancellationToken cancellationToken)
        {
            var input = PersonNameInput.Create(request.Name, request.FirstName, request.MiddleName, request.LastName);
            if (!input.IsSuccess)
            {
                return input.Error;
            }
            if (request.Version is not { } version)
            {
                return AppError.Validation("version", "Version is required. Read the person first and send its version.");
            }

            // IMMEDIATE transaction: the version check and the write cannot interleave with another writer.
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            var person = await _context.People.SingleOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
            if (person is null)
            {
                return AppError.NotFound("Person not found.");
            }

            // A stale version is a conflict even when the requested names already match.
            if (person.Version != version)
            {
                return AppError.Conflict(ErrorCodes.ConcurrencyConflict,
                    "This person was changed by another request. Reload and review before trying again.");
            }

            var names = input.Value;
            if (person.ReplaceNames(names.Name, names.FirstName, names.MiddleName, names.LastName))
            {
                await _context.SaveChangesAsync(cancellationToken);
            }

            // Read the summary inside the transaction so the response pairs it with this version.
            var detail = await _context.AstronautDetails.AsNoTracking()
                .SingleOrDefaultAsync(d => d.PersonId == person.Id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return PersonDtoMapper.ToDetail(person, detail);
        }
    }
}
