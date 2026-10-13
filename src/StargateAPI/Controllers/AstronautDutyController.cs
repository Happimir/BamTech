using MediatR;
using Microsoft.AspNetCore.Mvc;
using StargateAPI.Business.Commands;
using StargateAPI.Business.Queries;

namespace StargateAPI.Controllers
{
    /// <summary>Original name-based duty routes, kept as compatibility adapters.</summary>
    [ApiController]
    [Route("[controller]")]
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

        [HttpPost("")]
        public async Task<IActionResult> CreateAstronautDuty([FromBody] CreateAstronautDuty request, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(request, cancellationToken);
            return Ok(result);
        }

        private async Task<IActionResult> GetDutiesCore(string? name, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetPersonDuties(Name: name), cancellationToken);
            return this.ToActionResult(result, Ok);
        }
    }
}
