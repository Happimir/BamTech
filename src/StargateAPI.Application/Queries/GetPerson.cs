using MediatR;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Application.Abstractions;
using StargateAPI.Domain;
using StargateAPI.Application.Logging;
using StargateAPI.Application.Dtos;
using StargateAPI.Domain.Results;

namespace StargateAPI.Application.Queries
{
    /// <summary>Gets one person by stable ID or, for compatibility routes, by exact name.</summary>
    public sealed record GetPerson(Guid? Id = null, string? Name = null) : IRequest<Result<PersonDetailDto>>;

    public class GetPersonHandler : IRequestHandler<GetPerson, Result<PersonDetailDto>>
    {
        private readonly IStargateDbContext _context;
        private readonly OperationContext _operation;

        public GetPersonHandler(IStargateDbContext context, OperationContext operation)
        {
            _context = context;
            _operation = operation;
        }

        public async Task<Result<PersonDetailDto>> Handle(GetPerson request, CancellationToken cancellationToken)
        {
            Guid id;
            if (request.Id is { } requestedId)
            {
                id = requestedId;
            }
            else
            {
                var resolved = await PersonLookup.ResolveIdByNameAsync(_context, request.Name, cancellationToken);
                if (!resolved.IsSuccess)
                {
                    return resolved.Error;
                }
                id = resolved.Value;
            }

            _operation.PersonId = id;

            var person = await _context.People
                .AsNoTracking()
                .Where(p => p.Id == id)
                .Select(p => new PersonDetailDto(
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
                            p.AstronautDetail.CareerEndDate),
                    p.FirstName,
                    p.MiddleName,
                    p.LastName))
                .SingleOrDefaultAsync(cancellationToken);

            return person is null ? AppError.NotFound("Person not found.") : person;
        }
    }
}
