using Microsoft.EntityFrameworkCore;
using StargateAPI.Application.Abstractions;
using StargateAPI.Domain;

namespace StargateAPI.Application.Services
{
    /// <summary>
    /// Reads and stores a person's career around the domain rules in <see cref="CareerTransitions"/>.
    /// Call inside the write transaction, so the state read is the state the change is based on.
    /// </summary>
    public interface IAstronautCareers
    {
        /// <summary>The summary, the open duty and the latest start date the transition rules need.</summary>
        Task<CareerState> LoadStateAsync(Guid personId, CancellationToken cancellationToken);

        /// <summary>
        /// Saves the closure of the previous duty, then stages the new duty (and, for a first duty,
        /// the summary). The caller's next SaveChanges inserts them; both saves share the transaction.
        /// </summary>
        Task StageAsync(DutyAssignment assignment, CancellationToken cancellationToken);
    }

    public sealed class AstronautCareers : IAstronautCareers
    {
        private readonly IStargateDbContext _context;

        public AstronautCareers(IStargateDbContext context)
        {
            _context = context;
        }

        public async Task<CareerState> LoadStateAsync(Guid personId, CancellationToken cancellationToken) =>
            new(
                await _context.AstronautDetails.SingleOrDefaultAsync(d => d.PersonId == personId, cancellationToken),
                await _context.AstronautDuties.SingleOrDefaultAsync(d => d.PersonId == personId && d.DutyEndDate == null, cancellationToken),
                await _context.AstronautDuties.Where(d => d.PersonId == personId).MaxAsync(d => (DateOnly?)d.DutyStartDate, cancellationToken));

        public async Task StageAsync(DutyAssignment assignment, CancellationToken cancellationToken)
        {
            // The closure (and version/summary updates) is saved before the new open duty is added,
            // so the one-open-duty partial unique index never sees two open rows, whatever order EF
            // would choose for the commands.
            await _context.SaveChangesAsync(cancellationToken);

            _context.AstronautDuties.Add(assignment.NewDuty);
            if (assignment.NewDetail is { } newDetail)
            {
                _context.AstronautDetails.Add(newDetail);
            }
        }
    }
}
