using StargateAPI.Application.Dtos;
using StargateAPI.Domain;

namespace StargateAPI.Application.Queries
{
    /// <summary>
    /// The directory's name filter, order and row shape, shared by the paged list and the full
    /// stream so the two can never disagree about who matches or in which order.
    /// </summary>
    internal static class PeopleDirectory
    {
        /// <summary>
        /// Validates the optional name filter. Blank means no filter (null prefix); otherwise the
        /// prefix is the normalized key. False with a safe message for invalid input.
        /// </summary>
        public static bool TryPrefix(string? name, out string? prefix, out string? error)
        {
            prefix = null;
            error = null;
            if (string.IsNullOrWhiteSpace(name))
            {
                return true;
            }
            if (!NameNormalizer.TryNormalize(name, out var key, out error))
            {
                return false;
            }
            prefix = key;
            return true;
        }

        public static IQueryable<Person> Matching(IQueryable<Person> people, string? prefix) =>
            // EF translates StartsWith with a parameter to LIKE with the parameter's own %, _ and
            // escape characters escaped, so they match literally (pinned by tests).
            prefix is null ? people : people.Where(p => p.NormalizedName.StartsWith(prefix));

        /// <summary>Directory rows in the deterministic (NormalizedName, Id) order; namesakes tie-break by ID.</summary>
        public static IQueryable<PersonSummaryDto> InDirectoryOrder(IQueryable<Person> people) =>
            people
                .OrderBy(p => p.NormalizedName)
                .ThenBy(p => p.Id)
                .Select(p => new PersonSummaryDto(
                    p.Id,
                    p.Name,
                    p.Version,
                    p.AstronautDetail == null
                        ? null
                        : new AstronautSummaryDto(
                            p.AstronautDetail.CurrentRank,
                            p.AstronautDetail.CurrentDutyTitle,
                            p.AstronautDetail.CurrentDutyStartDate,
                            p.AstronautDetail.CareerStartDate,
                            p.AstronautDetail.CareerEndDate)));
    }
}
