using MediatR;
using Microsoft.AspNetCore.Mvc;
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
    }
}
