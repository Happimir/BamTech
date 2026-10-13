using MediatR;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Application.Abstractions;
using StargateAPI.Domain;
using StargateAPI.Application.Logging;
using StargateAPI.Application.Dtos;
using StargateAPI.Domain.Results;

namespace StargateAPI.Application.Queries
{
    /// <summary>Gets a person with their duty history (newest first), by stable ID or exact name.</summary>
    public sealed record GetPersonDuties(Guid? Id = null, string? Name = null) : IRequest<Result<PersonDutiesDto>>;

    public class GetPersonDutiesHandler : IRequestHandler<GetPersonDuties, Result<PersonDutiesDto>>
    {
        private readonly IStargateDbContext _context;
        private readonly OperationContext _operation;

        public GetPersonDutiesHandler(IStargateDbContext context, OperationContext operation)
        {
            _context = context;
            _operation = operation;
        }

        public async Task<Result<PersonDutiesDto>> Handle(GetPersonDuties request, CancellationToken cancellationToken)
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

            // One projection compiles to a single SQL statement (EF's default single-query mode),
            // so the summary and history come from the same database snapshot. Do not switch this
            // to AsSplitQuery without adding an explicit read transaction.
            var result = await _context.People
                .AsNoTracking()
                .Where(p => p.Id == id)
                .Select(p => new PersonDutiesDto(
                    new PersonDetailDto(
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
                        p.LastName),
                    p.AstronautDuties
                        .OrderByDescending(d => d.DutyStartDate)
                        .ThenByDescending(d => d.Id)
                        .Select(d => new DutyDto(d.Id, d.Rank, d.DutyTitle, d.DutyStartDate, d.DutyEndDate))
                        .ToList()))
                .SingleOrDefaultAsync(cancellationToken);

            return result is null ? AppError.NotFound("Person not found.") : result;
        }
    }
}
