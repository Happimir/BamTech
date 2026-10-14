namespace StargateAPI.Application.Dtos
{
    /// <summary>
    /// Body for POST /api/people/{id}/duties. End and career dates are derived by the server and
    /// are rejected if supplied (unknown fields are a 400).
    /// </summary>
    public sealed record AssignDutyDto(
        string? Rank,
        string? DutyTitle,
        DateOnly? DutyStartDate,
        Guid? ExpectedVersion = null);

    /// <summary>Body for the legacy POST /AstronautDuty: the same fields plus the person's exact name.</summary>
    public sealed record LegacyAssignDutyDto(
        string? Name,
        string? Rank,
        string? DutyTitle,
        DateOnly? DutyStartDate,
        Guid? ExpectedVersion = null);
}
