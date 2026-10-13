namespace StargateAPI.Business.Domain
{
    /// <summary>
    /// "Today" for business rules such as rejecting future-dated duties. UTC is a stated product
    /// convention for this exercise, not something inferred from the external source.
    /// </summary>
    public static class BusinessCalendar
    {
        public const string TimeZone = "UTC";

        public static DateOnly Today(TimeProvider timeProvider) =>
            DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
    }
}
