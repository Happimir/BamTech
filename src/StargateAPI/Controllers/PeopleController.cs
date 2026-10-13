using MediatR;
using Microsoft.AspNetCore.Mvc;
using StargateAPI.Business.Commands;
using StargateAPI.Business.Dtos;
using StargateAPI.Business.Queries;

namespace StargateAPI.Controllers
{
    /// <summary>Stable-ID routes. IDs are opaque UUIDs; the route constraint checks format only.</summary>
    [ApiController]
    [Route("api/people")]
    public class PeopleController : ControllerBase
    {
        private readonly IMediator _mediator;

        public PeopleController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetPerson(Guid id, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetPerson(Id: id), cancellationToken);
            return this.ToActionResult(result, Ok);
        }

        [HttpGet("{id:guid}/duties")]
        public async Task<IActionResult> GetPersonDuties(Guid id, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetPersonDuties(Id: id), cancellationToken);
            return this.ToActionResult(result, Ok);
        }

        /// <summary>The duty identified by a duty-create Location; 404 if it belongs to another person.</summary>
        [HttpGet("{id:guid}/duties/{dutyId:guid}")]
        public async Task<IActionResult> GetPersonDuty(Guid id, Guid dutyId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetPersonDuty(id, dutyId), cancellationToken);
            return this.ToActionResult(result, Ok);
        }

        /// <summary>
        /// Appends a duty. The previous duty is closed the day before; end and career dates are
        /// derived, never accepted. Supply expectedVersion to reject the write if the person changed.
        /// </summary>
        [HttpPost("{id:guid}/duties")]
        public async Task<IActionResult> AssignDuty(Guid id, [FromBody] AssignDutyDto request, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new AssignDuty(
                id,
                Name: null,
                request.Rank,
                request.DutyTitle,
                request.DutyStartDate,
                request.ExpectedVersion), cancellationToken);
            return this.ToActionResult(result, created =>
                Created($"/api/people/{created.PersonId}/duties/{created.Id}", created));
        }
    }
}
