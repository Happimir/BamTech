using MediatR;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Business.Data;
using StargateAPI.Business.Domain;
using StargateAPI.Business.Dtos;
using StargateAPI.Business.Logging;
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
        private readonly TimeProvider _timeProvider;
        private readonly OperationContext _operation;

        public UpdatePersonHandler(StargateContext context, TimeProvider timeProvider, OperationContext operation)
        {
            _context = context;
            _timeProvider = timeProvider;
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
                return AppError.Validation("version", "Version is required. Read the person first and send its version.");
            }

            // IMMEDIATE transaction: the version check and the write cannot interleave with another writer.
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            _operation.PersonId = request.Id;
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
            var before = (person.Name, person.FirstName, person.MiddleName, person.LastName);
            var changed = person.ReplaceNames(names.Name, names.FirstName, names.MiddleName, names.LastName);
            if (changed)
            {
                // Field names only: the audit never copies personal name values.
                var changedFields = new List<string>();
                if (before.Name != person.Name) changedFields.Add("name");
                if (before.FirstName != person.FirstName) changedFields.Add("firstName");
                if (before.MiddleName != person.MiddleName) changedFields.Add("middleName");
                if (before.LastName != person.LastName) changedFields.Add("lastName");

                _context.ProcessLogs.Add(ProcessLog.MutationAudit(
                    _operation, "UpdatePerson", _timeProvider.GetUtcNow(), person.Id, dutyId: null,
                    "Person name fields replaced.", LogContent.Changes(new { ChangedFields = changedFields })));
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
