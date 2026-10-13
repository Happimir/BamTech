using MediatR;
using Microsoft.AspNetCore.Mvc;
using StargateAPI.Business.Commands;
using StargateAPI.Business.Queries;

namespace StargateAPI.Controllers
{
    /// <summary>
    /// Original name-based routes, kept as compatibility adapters. Names are not unique: an
    /// ambiguous name returns 409 and callers should switch to the ID routes under /api/people.
    /// </summary>
    [ApiController]
    [Route("[controller]")]
    public class PersonController : ControllerBase
    {
        private readonly IMediator _mediator;
        public PersonController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("")]
        public async Task<IActionResult> GetPeople(CancellationToken cancellationToken)
        {
            return Ok(await _mediator.Send(new GetPeople(), cancellationToken));
        }

        /// <summary>Exact name lookup without path-segment limitations (slashes, reserved words).</summary>
        [HttpGet("by-name")]
        public Task<IActionResult> GetPersonByNameQuery([FromQuery] string? name, CancellationToken cancellationToken) =>
            GetPersonByNameCore(name, cancellationToken);

        [HttpGet("{name}")]
        public Task<IActionResult> GetPersonByName(string name, CancellationToken cancellationToken) =>
            GetPersonByNameCore(name, cancellationToken);

        /// <summary>Deprecated raw-string create; prefer POST /api/people with structured fields.</summary>
        [HttpPost("")]
        public async Task<IActionResult> CreatePerson([FromBody] string name, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new CreatePerson(name), cancellationToken);
            return this.ToActionResult(result, person => Created($"/api/people/{person.Id}", person));
        }

        private async Task<IActionResult> GetPersonByNameCore(string? name, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetPerson(Name: name), cancellationToken);
            return this.ToActionResult(result, Ok);
        }
    }
}
