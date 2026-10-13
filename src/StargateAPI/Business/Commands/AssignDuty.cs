using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Business.Data;
using StargateAPI.Business.Domain;
using StargateAPI.Business.Logging;
using StargateAPI.Business.Queries;
using StargateAPI.Business.Results;

namespace StargateAPI.Business.Commands
{
    /// <summary>
    /// Appends a duty to a person's history, identified by stable ID or (legacy route) by exact
    /// name. ExpectedVersion is optional; when supplied, a stale value is a conflict.
    /// </summary>
    public sealed record AssignDuty(
        Guid? PersonId,
        string? Name,
        string? Rank,
        string? DutyTitle,
        DateOnly? DutyStartDate,
        Guid? ExpectedVersion) : IRequest<Result<AssignDutyResult>>;

    /// <summary>The created duty, its person, and the person's new aggregate version.</summary>
    public sealed record AssignDutyResult(Guid Id, Guid PersonId, Guid Version);

    public class AssignDutyHandler : IRequestHandler<AssignDuty, Result<AssignDutyResult>>
    {
        // SQLITE_CONSTRAINT_UNIQUE extended result code.
        private const int SqliteConstraintUnique = 2067;

        private readonly StargateContext _context;
        private readonly TimeProvider _timeProvider;
        private readonly OperationContext _operation;

        public AssignDutyHandler(StargateContext context, TimeProvider timeProvider, OperationContext operation)
        {
            _context = context;
            _timeProvider = timeProvider;
            _operation = operation;
        }

        public async Task<Result<AssignDutyResult>> Handle(AssignDuty request, CancellationToken cancellationToken)
        {
            var input = DutyAssignmentInput.Create(
                request.Rank, request.DutyTitle, request.DutyStartDate, BusinessCalendar.Today(_timeProvider));
            if (!input.IsSuccess)
            {
                return input.Error;
            }

            // Microsoft.Data.Sqlite begins an IMMEDIATE transaction by default, taking the write
            // lock before any state is read: competing writers serialize here rather than both
            // reading the same "current" duty. Disposal without commit rolls everything back.
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            var personId = request.PersonId;
            if (personId is null)
            {
                var resolved = await PersonLookup.ResolveIdByNameAsync(_context, request.Name, cancellationToken);
                if (!resolved.IsSuccess)
                {
                    return resolved.Error;
                }
                personId = resolved.Value;
            }

            _operation.PersonId = personId;
            var person = await _context.People.SingleOrDefaultAsync(p => p.Id == personId, cancellationToken);
            if (person is null)
            {
                return AppError.NotFound("Person not found.");
            }

            if (request.ExpectedVersion is { } expected && expected != person.Version)
            {
                return AppError.Conflict(ErrorCodes.ConcurrencyConflict,
                    "This person was changed by another request. Reload and review before trying again.");
            }

            var state = new CareerState(
                await _context.AstronautDetails.SingleOrDefaultAsync(d => d.PersonId == person.Id, cancellationToken),
                await _context.AstronautDuties.SingleOrDefaultAsync(d => d.PersonId == person.Id && d.DutyEndDate == null, cancellationToken),
                await _context.AstronautDuties.Where(d => d.PersonId == person.Id).MaxAsync(d => (DateOnly?)d.DutyStartDate, cancellationToken));

            var assignment = CareerTransitions.AssignDuty(person, state, input.Value);
            if (!assignment.IsSuccess)
            {
                return assignment.Error;
            }

            try
            {
                // Save the closure (and version/summary updates) before inserting the new open duty,
                // so the one-open-duty partial unique index never sees two open rows regardless of
                // EF's command ordering. Both saves commit or roll back together.
                await _context.SaveChangesAsync(cancellationToken);

                var newDuty = assignment.Value.NewDuty;
                _context.AstronautDuties.Add(newDuty);
                if (assignment.Value.NewDetail is { } newDetail)
                {
                    _context.AstronautDetails.Add(newDetail);
                }

                // The audit commits with the change or not at all.
                _operation.AstronautDutyId = newDuty.Id;
                _context.ProcessLogs.Add(ProcessLog.MutationAudit(
                    _operation, "AssignDuty", _timeProvider.GetUtcNow(), person.Id, newDuty.Id,
                    "Duty assigned.",
                    LogContent.Changes(new
                    {
                        newDuty.DutyTitle,
                        newDuty.Rank,
                        newDuty.DutyStartDate,
                        FirstAssignment = assignment.Value.NewDetail is not null,
                        ClosedDutyId = assignment.Value.ClosedDuty?.Id,
                        ClosedDutyEndDate = assignment.Value.ClosedDuty?.DutyEndDate,
                        CareerEndDate = (state.Detail ?? assignment.Value.NewDetail)?.CareerEndDate
                    })));
                await _context.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
                _operation.MutationCommitted = true;
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteExtendedErrorCode: SqliteConstraintUnique })
            {
                // Only reachable if something bypassed the transaction (e.g. a direct write).
                return AppError.Conflict(ErrorCodes.ChronologyConflict,
                    "The duty conflicts with an existing duty for this person. Reload and review.");
            }

            return new AssignDutyResult(assignment.Value.NewDuty.Id, person.Id, person.Version);
        }
    }
}
