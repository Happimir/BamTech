using MediatR;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Application.Abstractions;
using StargateAPI.Domain;
using StargateAPI.Application.Logging;
using StargateAPI.Application.Dtos;
using StargateAPI.Domain.Results;

namespace StargateAPI.Application.Queries
{
    /// <summary>Gets one duty, which must belong to the given person (the target of a duty create Location).</summary>
    public sealed record GetPersonDuty(Guid PersonId, Guid DutyId) : IRequest<Result<DutyDto>>;

    public class GetPersonDutyHandler : IRequestHandler<GetPersonDuty, Result<DutyDto>>
    {
        private readonly IStargateDbContext _context;

        public GetPersonDutyHandler(IStargateDbContext context)
        {
            _context = context;
        }

        public async Task<Result<DutyDto>> Handle(GetPersonDuty request, CancellationToken cancellationToken)
        {
            var duty = await _context.AstronautDuties
                .AsNoTracking()
                .Where(d => d.Id == request.DutyId && d.PersonId == request.PersonId)
                .Select(d => new DutyDto(d.Id, d.Rank, d.DutyTitle, d.DutyStartDate, d.DutyEndDate))
                .SingleOrDefaultAsync(cancellationToken);

            return duty is null ? AppError.NotFound("Duty not found for this person.") : duty;
        }
    }
}
