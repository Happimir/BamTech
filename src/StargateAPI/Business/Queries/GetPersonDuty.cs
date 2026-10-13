using MediatR;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Business.Data;
using StargateAPI.Business.Dtos;
using StargateAPI.Business.Results;

namespace StargateAPI.Business.Queries
{
    /// <summary>Gets one duty, which must belong to the given person (the target of a duty create Location).</summary>
    public sealed record GetPersonDuty(Guid PersonId, Guid DutyId) : IRequest<Result<DutyDto>>;

    public class GetPersonDutyHandler : IRequestHandler<GetPersonDuty, Result<DutyDto>>
    {
        private readonly StargateContext _context;

        public GetPersonDutyHandler(StargateContext context)
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
