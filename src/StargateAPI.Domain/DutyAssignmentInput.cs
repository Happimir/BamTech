using StargateAPI.Domain.Results;

namespace StargateAPI.Domain
{
    /// <summary>A validated, canonical duty assignment request.</summary>
    public sealed record DutyAssignmentInput(string Rank, string DutyTitle, DateOnly StartDate)
    {
        public const string RetiredTitle = "RETIRED";

        public bool IsRetirement => Retirement.IsRetirementTitle(DutyTitle);

        /// <summary>
        /// Shape validation: trimmed nonblank bounded rank/title, a required start date that is not
        /// in the future (business date) and can safely have one day subtracted. A case-insensitive
        /// "retired" title is canonicalized to RETIRED; other titles keep their spelling.
        /// </summary>
        public static Result<DutyAssignmentInput> Create(string? rank, string? dutyTitle, DateOnly? startDate, DateOnly businessToday)
        {
            var errors = new Dictionary<string, string[]>();

            var trimmedRank = rank?.Trim() ?? string.Empty;
            if (trimmedRank.Length == 0)
            {
                errors["rank"] = ["Rank is required."];
            }
            else if (trimmedRank.Length > AstronautDetail.MaxRankLength)
            {
                errors["rank"] = [$"Rank must be at most {AstronautDetail.MaxRankLength} characters."];
            }

            var trimmedTitle = dutyTitle?.Trim() ?? string.Empty;
            if (trimmedTitle.Length == 0)
            {
                errors["dutyTitle"] = ["Duty title is required."];
            }
            else if (trimmedTitle.Length > AstronautDetail.MaxDutyTitleLength)
            {
                errors["dutyTitle"] = [$"Duty title must be at most {AstronautDetail.MaxDutyTitleLength} characters."];
            }
            else if (string.Equals(trimmedTitle, RetiredTitle, StringComparison.OrdinalIgnoreCase))
            {
                trimmedTitle = RetiredTitle;
            }

            if (startDate is not { } start)
            {
                errors["dutyStartDate"] = ["Duty start date is required (YYYY-MM-DD)."];
            }
            else if (start == DateOnly.MinValue)
            {
                errors["dutyStartDate"] = ["Duty start date is out of range."];
            }
            else if (start > businessToday)
            {
                errors["dutyStartDate"] = [$"Duty start date cannot be in the future (business date {businessToday:yyyy-MM-dd} {BusinessCalendar.TimeZone})."];
            }

            if (errors.Count > 0)
            {
                return AppError.Validation(errors);
            }

            return new DutyAssignmentInput(trimmedRank, trimmedTitle, startDate!.Value);
        }
    }
}
