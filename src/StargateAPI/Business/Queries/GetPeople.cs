using MediatR;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Business.Data;
using StargateAPI.Business.Dtos;

namespace StargateAPI.Business.Queries
{
    public class GetPeople : IRequest<IReadOnlyList<PersonSummaryDto>>
    {

    }

    public class GetPeopleHandler : IRequestHandler<GetPeople, IReadOnlyList<PersonSummaryDto>>
    {
        private readonly StargateContext _context;

        public GetPeopleHandler(StargateContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<PersonSummaryDto>> Handle(GetPeople request, CancellationToken cancellationToken)
        {
            return await _context.People
                .AsNoTracking()
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
                            p.AstronautDetail.CareerEndDate)))
                .ToListAsync(cancellationToken);
        }
    }
}
