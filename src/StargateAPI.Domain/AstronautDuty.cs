
namespace StargateAPI.Domain
{
    /// <summary>One assignment in a person's history. The open duty (no end date) is the current one.</summary>
    public class AstronautDuty
    {
        // For EF Core materialization.
        private AstronautDuty()
        {
        }

        public Guid Id { get; private set; }

        public Guid PersonId { get; private set; }

        public string Rank { get; private set; } = string.Empty;

        public string DutyTitle { get; private set; } = string.Empty;

        public DateOnly DutyStartDate { get; private set; }

        public DateOnly? DutyEndDate { get; private set; }

        public virtual Person Person { get; private set; } = null!;

        /// <summary>Creates a new open (current) duty with an application-generated UUIDv7.</summary>
        internal static AstronautDuty Start(Guid personId, string rank, string dutyTitle, DateOnly startDate) => new()
        {
            Id = Guid.CreateVersion7(),
            PersonId = personId,
            Rank = rank,
            DutyTitle = dutyTitle,
            DutyStartDate = startDate,
            DutyEndDate = null
        };

        /// <summary>Closing a duty is the only change ever made to an existing duty (rule 5).</summary>
        internal void Close(DateOnly endDate) => DutyEndDate = endDate;
    }
}
