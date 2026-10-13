namespace StargateAPI.Application.Dtos
{
    /// <summary>Body for POST /api/people. Omitted name parts are null. The server generates the ID.</summary>
    public sealed record CreatePersonDto(
        string? Name,
        string? FirstName = null,
        string? MiddleName = null,
        string? LastName = null);

    /// <summary>
    /// Body for PUT /api/people/{id}: the complete name fields (omitted parts become null) plus the
    /// version read from the person, which is required.
    /// </summary>
    public sealed record UpdatePersonDto(
        string? Name,
        string? FirstName,
        string? MiddleName,
        string? LastName,
        Guid? Version);

    /// <summary>
    /// Body for the legacy PUT /Person/by-name?name=...: the requested display name, plus
    /// expectedVersion when that changes an existing person's name. No structured parts.
    /// </summary>
    public sealed record NameUpdateDto(string? Name, Guid? ExpectedVersion = null);

    /// <summary>Response for GET /api/metadata.</summary>
    public sealed record BusinessMetadataDto(DateOnly BusinessDate, string BusinessTimeZone);
}
