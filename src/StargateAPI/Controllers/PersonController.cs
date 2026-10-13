using MediatR;
using Microsoft.AspNetCore.Mvc;
using StargateAPI.Business.Commands;
using StargateAPI.Business.Dtos;
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

        /// <summary>
        /// All people, one page at a time (default 25, maximum 100). Changed from the starter's
        /// unbounded list; traverse pages to retrieve everyone. Same query as GET /api/people.
        /// </summary>
        [HttpGet("")]
        public async Task<IActionResult> GetPeople(
            [FromQuery] int? page, [FromQuery] int? pageSize, [FromQuery] string? name, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetPeople(page, pageSize, name), cancellationToken);
            return this.ToActionResult(result, Ok);
        }

        /// <summary>
        /// Add or update a person by exact name: 201 when created, 200 when updated or unchanged.
        /// Renaming requires expectedVersion; an ambiguous name is a 409 (use PUT /api/people/{id}).
        /// </summary>
        [HttpPut("by-name")]
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
