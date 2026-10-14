using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StargateAPI.Application.Commands;
using StargateAPI.Application.Dtos;
using StargateAPI.Application.Queries;
using StargateAPI.Security;

namespace StargateAPI.Controllers
{
    /// <summary>
    /// Original name-based routes, kept as compatibility adapters. Names are not unique: an
    /// ambiguous name returns 409 and callers should switch to the ID routes under /api/people.
    /// </summary>
    [ApiController]
    [Route("[controller]")]
    [Authorize(Policy = Policies.PeopleRead)]
    public class PersonController : ControllerBase
    {
        private readonly IMediator _mediator;
        public PersonController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>
        /// Every person, as a JSON array streamed while it is read (server memory stays flat however
        /// many people there are), in directory order; optionally filtered by a case-insensitive
        /// name prefix. For a UI, use the paged GET /api/people. A failure after streaming has
        /// started ends the response early (the 200 is already sent) and is logged as Failed.
        /// </summary>
        [HttpGet("")]
        [ProducesResponseType<IEnumerable<PersonSummaryDto>>(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPeople([FromQuery] string? name, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetAllPeople(name), cancellationToken);
            return this.ToActionResult(result, Ok);
        }

        /// <summary>
        /// Add or update a person by exact name: 201 when created, 200 when updated or unchanged.
        /// Renaming requires expectedVersion; an ambiguous name is a 409 (use PUT /api/people/{id}).
        /// </summary>
        [HttpPut("by-name")]
        [Authorize(Policy = Policies.PeopleWrite)]
        public async Task<IActionResult> UpsertPersonByName(
            [FromQuery] string? name, [FromBody] NameUpdateDto request, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new UpsertPersonByName(name, request.Name, request.ExpectedVersion), cancellationToken);
            return this.ToActionResult(result, upsert => upsert.Created
                ? Created($"/api/people/{upsert.Person.Id}", upsert.Person)
                : Ok(upsert.Person));
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
        [Authorize(Policy = Policies.PeopleWrite)]
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
