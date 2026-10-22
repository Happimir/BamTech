namespace StargateAPI.Application.Dtos
{
    /// <summary>The business calendar: the server's "today" for date rules, and its zone (UTC).</summary>
    public sealed record BusinessMetadataDto(DateOnly BusinessDate, string BusinessTimeZone);
}
