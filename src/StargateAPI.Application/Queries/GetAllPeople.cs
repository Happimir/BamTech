using MediatR;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Application.Abstractions;
using StargateAPI.Application.Dtos;
using StargateAPI.Domain.Results;

namespace StargateAPI.Application.Queries
{
    /// <summary>
    /// Every person (optionally filtered by name prefix), in directory order, as a stream: rows are
    /// read from the database while the response is written, so memory does not grow with the
    /// number of people. Paging remains the right tool for a UI; this serves "retrieve all people".
    /// </summary>
    public sealed record GetAllPeople(string? Name = null) : IRequest<Result<IAsyncEnumerable<PersonSummaryDto>>>;

    public class GetAllPeopleHandler : IRequestHandler<GetAllPeople, Result<IAsyncEnumerable<PersonSummaryDto>>>
    {
        private readonly IStargateDbContext _context;

        public GetAllPeopleHandler(IStargateDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Validates before anything is sent, so a bad filter is still a 400. The returned sequence
        /// is lazy: the query runs as it is enumerated, inside the request, on the request's
        /// context. Once streaming has started a failure can no longer become an error response.
        /// </summary>
        public Task<Result<IAsyncEnumerable<PersonSummaryDto>>> Handle(GetAllPeople request, CancellationToken cancellationToken)
        {
            if (!PeopleDirectory.TryPrefix(request.Name, out var prefix, out var nameError))
            {
                return Task.FromResult(Result<IAsyncEnumerable<PersonSummaryDto>>.Failure(AppError.Validation("name", nameError!)));
            }

            var people = PeopleDirectory.InDirectoryOrder(
                PeopleDirectory.Matching(_context.People.AsNoTracking(), prefix));
            return Task.FromResult(Result<IAsyncEnumerable<PersonSummaryDto>>.Success(people.AsAsyncEnumerable()));
        }
    }
}
