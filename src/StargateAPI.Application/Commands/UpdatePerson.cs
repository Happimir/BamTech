using MediatR;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Application.Abstractions;
using StargateAPI.Domain;
using StargateAPI.Application.Logging;
using StargateAPI.Application.Dtos;
using StargateAPI.Application.Services;
using StargateAPI.Domain.Results;

namespace StargateAPI.Application.Commands
{
    /// <summary>
    /// Replaces a person's name fields by stable ID. Version is required: a missing version is a
    /// 428 and a stale one is a 409. Identity and duty history are unaffected by a rename.
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
        private readonly IStargateDbContext _context;
        private readonly IWriteTransactions _transactions;
        private readonly IAuditTrail _audit;
        private readonly OperationContext _operation;

        public UpdatePersonHandler(IStargateDbContext context, IWriteTransactions transactions, IAuditTrail audit, OperationContext operation)
        {
            _context = context;
            _transactions = transactions;
            _audit = audit;
            _operation = operation;
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
                return AppError.VersionRequired();
            }

            // Write-locked transaction: the version check and the write cannot interleave with another writer.
            await using var transaction = await _transactions.BeginAsync(cancellationToken);

            _operation.PersonId = request.Id;
            var person = await _context.People.SingleOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
            if (person is null)
            {
                return AppError.NotFound("Person not found.");
            }

            // A stale version is a conflict even when the requested names already match.
            if (person.IsStale(version))
            {
                return AppError.StaleVersion();
            }

            var names = input.Value;
            var changedFields = person.ReplaceNames(names.Name, names.FirstName, names.MiddleName, names.LastName);
            var changed = changedFields.Count > 0;
            if (changed)
            {
                // Field names only: the audit never copies personal name values.
                _audit.Record("UpdatePerson", person.Id, dutyId: null, "Person name fields replaced.",
                    new { ChangedFields = changedFields });
                await _context.SaveChangesAsync(cancellationToken);
            }

            // Read the summary inside the transaction so the response pairs it with this version.
            var detail = await _context.AstronautDetails.AsNoTracking()
                .SingleOrDefaultAsync(d => d.PersonId == person.Id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            _operation.MutationCommitted = changed;

            return PersonDtoMapper.ToDetail(person, detail);
        }
    }
}
