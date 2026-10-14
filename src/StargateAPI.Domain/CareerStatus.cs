namespace StargateAPI.Domain
{
    /// <summary>Where a person with astronaut records stands (a person with none has no status).</summary>
    public enum CareerStatus
    {
        Active,
        Retired
    }

    /// <summary>
    /// Rule 6 in one place: a duty is a retirement when its title is the canonical RETIRED, and a
    /// person is retired while their current duty is one. Clients read the result from the API
    /// (astronaut.status, duty.isRetirement) rather than re-deriving it from titles.
    /// </summary>
    public static class Retirement
    {
        public static bool IsRetirementTitle(string dutyTitle) => dutyTitle == DutyAssignmentInput.RetiredTitle;

        public static CareerStatus StatusOf(string currentDutyTitle) =>
            IsRetirementTitle(currentDutyTitle) ? CareerStatus.Retired : CareerStatus.Active;
    }
}
