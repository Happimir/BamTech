using MediatR;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Application.Abstractions;
using StargateAPI.Domain;
using StargateAPI.Application.Logging;
using StargateAPI.Application.Dtos;
using StargateAPI.Domain.Results;

namespace StargateAPI.Application.Queries
{
    /// <summary>
    /// Lists people one page at a time; every person is reachable by traversing pages. Name is an
    /// optional normalized prefix filter, matched literally (% and _ are ordinary characters).
    /// </summary>
    public sealed record GetPeople(int? Page = null, int? PageSize = null, string? Name = null)
        : IRequest<Result<PagedResponse<PersonSummaryDto>>>;

    public class GetPeopleHandler : IRequestHandler<GetPeople, Result<PagedResponse<PersonSummaryDto>>>
    {
        public const int DefaultPageSize = 25;
        public const int MaxPageSize = 100;

        private readonly IStargateDbContext _context;

        public GetPeopleHandler(IStargateDbContext context)
        {
            _context = context;
        }

        public async Task<Result<PagedResponse<PersonSummaryDto>>> Handle(GetPeople request, CancellationToken cancellationToken)
        {
            var page = request.Page ?? 1;
            var pageSize = request.PageSize ?? DefaultPageSize;

            var errors = new Dictionary<string, string[]>();
            if (page < 1)
            {
                errors["page"] = ["Page must be 1 or greater."];
            }
            if (pageSize < 1 || pageSize > MaxPageSize)
            {
                errors["pageSize"] = [$"Page size must be between 1 and {MaxPageSize}."];
            }
            else if (page >= 1 && (long)(page - 1) * pageSize > int.MaxValue)
            {
                errors["page"] = ["Page is out of range."];
            }

            string? prefix = null;
            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                if (NameNormalizer.TryNormalize(request.Name, out var key, out var nameError))
                {
                    prefix = key;
                }
                else
                {
                    errors["name"] = [nameError!];
                }
            }

            if (errors.Count > 0)
            {
                return AppError.Validation(errors);
            }

            var people = _context.People.AsNoTracking();
            if (prefix is not null)
            {
                // EF translates StartsWith with a parameter to LIKE with the parameter's own %, _
                // and escape characters escaped, so they match literally (pinned by tests).
                people = people.Where(p => p.NormalizedName.StartsWith(prefix));
            }

            // Count and page are separate queries: a live view, not a cross-request snapshot.
            var totalCount = await people.CountAsync(cancellationToken);
            var items = await people
                .OrderBy(p => p.NormalizedName)
                .ThenBy(p => p.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
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

            var totalPages = totalCount == 0 ? 0 : (int)((totalCount + (long)pageSize - 1) / pageSize);
            return new PagedResponse<PersonSummaryDto>(items, page, pageSize, totalCount, totalPages);
        }
    }
}
