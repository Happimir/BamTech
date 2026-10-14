using StargateAPI.Domain;
using StargateAPI.Application.Logging;

namespace StargateAPI.Application.Dtos
{
    /// <summary>Maps already-loaded entities to response DTOs after a write. Reads use query projections instead.</summary>
    public static class PersonDtoMapper
    {
        public static PersonDetailDto ToDetail(Person person, AstronautDetail? detail) => new(
            person.Id,
            person.Name,
            person.Version,
            detail is null
                ? null
                : new AstronautSummaryDto(
                    detail.CurrentRank,
                    detail.CurrentDutyTitle,
                    detail.CurrentDutyStartDate,
                    detail.CareerStartDate,
                    detail.CareerEndDate),
            person.FirstName,
            person.MiddleName,
            person.LastName);
    }
}
