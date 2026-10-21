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
        /// <summary>Active or retired (rule 6), decided by the server so clients never parse titles.</summary>
        public CareerStatus Status => Retirement.StatusOf(CurrentDutyTitle);
    }

    /// <summary>
    /// A person: stable ID, display name, version, optional current summary and the explicitly
    /// supplied structured name parts. Directory rows and single-person reads share this shape.
    /// </summary>
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
}
