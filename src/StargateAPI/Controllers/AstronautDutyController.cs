using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StargateAPI.Business.Commands;
using StargateAPI.Business.Dtos;
using StargateAPI.Business.Queries;
using StargateAPI.Security;

namespace StargateAPI.Controllers
{
    /// <summary>Original name-based duty routes, kept as compatibility adapters.</summary>
    [ApiController]
    [Route("[controller]")]
    [Authorize(Policy = Policies.PeopleRead)]
    public class AstronautDutyController : ControllerBase
    {
        private readonly IMediator _mediator;
        public AstronautDutyController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>Duties by exact name without path-segment limitations.</summary>
        [HttpGet("by-name")]
        public Task<IActionResult> GetAstronautDutiesByNameQuery([FromQuery] string? name, CancellationToken cancellationToken) =>
            GetDutiesCore(name, cancellationToken);

        [HttpGet("{name}")]
        public Task<IActionResult> GetAstronautDutiesByName(string name, CancellationToken cancellationToken) =>
            GetDutiesCore(name, cancellationToken);

        /// <summary>
        /// Legacy name-based duty append. The name is resolved inside the write transaction; an
        /// ambiguous name is rejected before anything changes. Prefer POST /api/people/{id}/duties.
        /// </summary>
        [HttpPost("")]
        [Authorize(Policy = Policies.DutiesWrite)]
        public async Task<IActionResult> CreateAstronautDuty([FromBody] LegacyAssignDutyDto request, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new AssignDuty(
                PersonId: null,
                request.Name,
                request.Rank,
                request.DutyTitle,
                request.DutyStartDate,
                request.ExpectedVersion), cancellationToken);
            return this.ToActionResult(result, created =>
                Created($"/api/people/{created.PersonId}/duties/{created.Id}", created));
        }

        private async Task<IActionResult> GetDutiesCore(string? name, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetPersonDuties(Name: name), cancellationToken);
            return this.ToActionResult(result, Ok);
        }
    }
}
