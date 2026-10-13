using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StargateAPI.Domain;
using StargateAPI.Application.Dtos;
using StargateAPI.Security;

namespace StargateAPI.Controllers
{
    /// <summary>
    /// Business calendar information for clients (e.g. the latest allowed duty start date).
    /// Informational only: the server re-checks the date on every submission.
    /// </summary>
    [ApiController]
    [Route("api/metadata")]
    [Authorize(Policy = Policies.PeopleRead)]
    public class MetadataController : ControllerBase
    {
        private readonly TimeProvider _timeProvider;

        public MetadataController(TimeProvider timeProvider)
        {
            _timeProvider = timeProvider;
        }

        [HttpGet("")]
        public IActionResult Get() =>
            Ok(new BusinessMetadataDto(BusinessCalendar.Today(_timeProvider), BusinessCalendar.TimeZone));
    }
}
