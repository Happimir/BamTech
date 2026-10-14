using System.Text.Json;
using System.Text.Json.Serialization;
using StargateAPI.Domain;

namespace StargateAPI.Application.Dtos
{
    /// <summary>Current assignment summary; null on a person with no assignments.</summary>
    public sealed record AstronautSummaryDto(
        string CurrentRank,
        string CurrentDutyTitle,
        DateOnly CurrentDutyStartDate,
        DateOnly CareerStartDate,
        DateOnly? CareerEndDate)
    {
        /// <summary>"active" or "retired" (rule 6), decided by the server so clients never parse titles.</summary>
        [JsonConverter(typeof(CamelCaseEnumConverter<CareerStatus>))]
        public CareerStatus Status => Retirement.StatusOf(CurrentDutyTitle);
    }

    /// <summary>Directory row: stable ID, display name, version and optional current summary.</summary>
    public sealed record PersonSummaryDto(
        Guid Id,
        string Name,
        Guid Version,
        AstronautSummaryDto? Astronaut);

    /// <summary>Single-person view: the summary plus explicitly supplied structured name parts.</summary>
    public sealed record PersonDetailDto(
        Guid Id,
        string Name,
        Guid Version,
        AstronautSummaryDto? Astronaut,
        string? FirstName,
        string? MiddleName,
        string? LastName);

    public sealed record DutyDto(
        Guid Id,
        string Rank,
        string DutyTitle,
        DateOnly DutyStartDate,
        DateOnly? DutyEndDate)
    {
        /// <summary>True for a retirement duty (rule 6), decided by the server.</summary>
        public bool IsRetirement => Retirement.IsRetirementTitle(DutyTitle);
    }

    /// <summary>A person and their duty history, newest first, read as one consistent snapshot.</summary>
    public sealed record PersonDutiesDto(
        PersonDetailDto Person,
        IReadOnlyList<DutyDto> Duties);

    /// <summary>Serializes an enum as its camelCase name ("retired"), matching the rest of the JSON.</summary>
    public sealed class CamelCaseEnumConverter<TEnum>() : JsonStringEnumConverter<TEnum>(JsonNamingPolicy.CamelCase)
        where TEnum : struct, Enum;
}
