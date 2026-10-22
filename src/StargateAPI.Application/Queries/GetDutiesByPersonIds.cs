using MediatR;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Application.Abstractions;
using StargateAPI.Application.Dtos;

namespace StargateAPI.Application.Queries
{
    /// <summary>
    /// Duty histories for several people in one query, newest first, keyed by person ID. People
    /// with no duties get an empty list. Serves the GraphQL duties DataLoader, so a page of people
    /// that asks for duties costs one query rather than one per person.
    /// </summary>
    public sealed record GetDutiesByPersonIds(IReadOnlyList<Guid> PersonIds)
        : IRequest<IReadOnlyDictionary<Guid, IReadOnlyList<DutyDto>>>;

    public class GetDutiesByPersonIdsHandler
        : IRequestHandler<GetDutiesByPersonIds, IReadOnlyDictionary<Guid, IReadOnlyList<DutyDto>>>
    {
        private readonly IStargateDbContext _context;

        public GetDutiesByPersonIdsHandler(IStargateDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<DutyDto>>> Handle(
            GetDutiesByPersonIds request, CancellationToken cancellationToken)
        {
            var rows = await _context.AstronautDuties
                .AsNoTracking()
                .Where(d => request.PersonIds.Contains(d.PersonId))
                .OrderByDescending(d => d.DutyStartDate)
                .ThenByDescending(d => d.Id)
                .Select(d => new { d.PersonId, Duty = new DutyDto(d.Id, d.Rank, d.DutyTitle, d.DutyStartDate, d.DutyEndDate) })
                .ToListAsync(cancellationToken);

            var byPerson = rows.ToLookup(r => r.PersonId, r => r.Duty);
            return request.PersonIds.Distinct().ToDictionary(
                id => id, IReadOnlyList<DutyDto> (id) => byPerson[id].ToList());
        }
    }
}
