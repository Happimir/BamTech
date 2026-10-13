using Microsoft.AspNetCore.Mvc;
using StargateAPI.Business.Domain;
using StargateAPI.Business.Dtos;

namespace StargateAPI.Controllers
{
    /// <summary>
    /// Business calendar information for clients (e.g. the latest allowed duty start date).
    /// Informational only: the server re-checks the date on every submission.
    /// </summary>
    [ApiController]
    [Route("api/metadata")]
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
