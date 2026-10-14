using MediatR;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Application.Abstractions;
using StargateAPI.Domain;
using StargateAPI.Application.Logging;
using StargateAPI.Application.Queries;
using StargateAPI.Application.Services;
using StargateAPI.Domain.Results;

namespace StargateAPI.Application.Commands
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

    /// <summary>
    /// Orchestrates the append: validate, lock, load, apply the domain rules
    /// (<see cref="CareerTransitions"/>), then save the change and its audit in one transaction.
    /// </summary>
    public class AssignDutyHandler : IRequestHandler<AssignDuty, Result<AssignDutyResult>>
    {
        private readonly IStargateDbContext _context;
        private readonly IWriteTransactions _transactions;
        private readonly IPersistenceErrors _errors;
        private readonly IAstronautCareers _careers;
        private readonly IAuditTrail _audit;
        private readonly TimeProvider _timeProvider;
        private readonly OperationContext _operation;

        public AssignDutyHandler(
            IStargateDbContext context,
            IWriteTransactions transactions,
            IPersistenceErrors errors,
            IAstronautCareers careers,
            IAuditTrail audit,
            TimeProvider timeProvider,
            OperationContext operation)
        {
            _context = context;
            _transactions = transactions;
            _errors = errors;
            _careers = careers;
            _audit = audit;
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

            // The write lock is taken before any state is read: competing writers serialize here
            // rather than both reading the same "current" duty. Disposal without commit rolls back.
            await using var transaction = await _transactions.BeginAsync(cancellationToken);

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
            if (person.IsStale(request.ExpectedVersion))
            {
                return AppError.StaleVersion();
            }

            var state = await _careers.LoadStateAsync(person.Id, cancellationToken);
            var assignment = CareerTransitions.AssignDuty(person, state, input.Value);
            if (!assignment.IsSuccess)
            {
                return assignment.Error;
            }

            var change = assignment.Value;
            try
            {
                await _careers.StageAsync(change, cancellationToken);

                _operation.AstronautDutyId = change.NewDuty.Id;
                _audit.Record("AssignDuty", person.Id, change.NewDuty.Id, "Duty assigned.", new
                {
                    change.NewDuty.DutyTitle,
                    change.NewDuty.Rank,
                    change.NewDuty.DutyStartDate,
                    FirstAssignment = change.NewDetail is not null,
                    ClosedDutyId = change.ClosedDuty?.Id,
                    ClosedDutyEndDate = change.ClosedDuty?.DutyEndDate,
                    CareerEndDate = (state.Detail ?? change.NewDetail)?.CareerEndDate
                });
                await _context.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
                _operation.MutationCommitted = true;
            }
            catch (DbUpdateException ex) when (_errors.IsUniqueViolation(ex))
            {
                // Only reachable if something bypassed the transaction (e.g. a direct write).
                return AppError.Conflict(ErrorCodes.ChronologyConflict,
                    "The duty conflicts with an existing duty for this person. Reload and review.");
            }

            return new AssignDutyResult(change.NewDuty.Id, person.Id, person.Version);
        }
    }
}
