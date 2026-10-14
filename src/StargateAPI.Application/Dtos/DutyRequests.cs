using System.Text.Json;
using System.Text.Json.Serialization;

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

    /// <summary>
    /// Body for the legacy POST /AstronautDuty: the same fields plus the person's exact name. The
    /// start date also accepts the starter's date-time form (see <see cref="LegacyCalendarDateConverter"/>).
    /// </summary>
    public sealed record LegacyAssignDutyDto(
        string? Name,
        string? Rank,
        string? DutyTitle,
        [property: JsonConverter(typeof(LegacyCalendarDateConverter))] DateOnly? DutyStartDate,
        Guid? ExpectedVersion = null);

    /// <summary>
    /// The starter's duty contract sent a DateTime ("2024-01-01T00:00:00"). The legacy route still
    /// accepts it, alongside "YYYY-MM-DD": any ISO 8601 date or date-time, with or without an
    /// offset, whose calendar date is taken exactly as written, with no time-zone conversion.
    /// Anything else is a 400. The ID route stays strict YYYY-MM-DD.
    /// </summary>
    public sealed class LegacyCalendarDateConverter : JsonConverter<DateOnly?>
    {
        public override DateOnly? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // DateTimeOffset keeps the clock time as written; a DateTime read would shift a value
            // with an offset into the server's local time and could change the day.
            if (reader.TokenType == JsonTokenType.String && reader.TryGetDateTimeOffset(out var value))
            {
                return DateOnly.FromDateTime(value.DateTime);
            }
            throw new JsonException("Duty start date must be YYYY-MM-DD or an ISO 8601 date-time.");
        }

        /// <summary>Request bodies are only ever read.</summary>
        public override void Write(Utf8JsonWriter writer, DateOnly? value, JsonSerializerOptions options) =>
            throw new NotSupportedException("The legacy duty request is never serialized.");
    }
}
