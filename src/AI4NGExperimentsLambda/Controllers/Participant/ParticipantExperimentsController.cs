using Microsoft.AspNetCore.Mvc;
using AI4NGExperimentManagement.Shared;

namespace AI4NGExperimentsLambda.Controllers.Participant
{
    /// <summary>
    /// Participant endpoints for viewing enrolled experiments and fetching an experiment bundle.
    /// </summary>
    [Route("api/me/experiments")]
    public class ParticipantExperimentsController : BaseApiController
    {
        public ParticipantExperimentsController(IAuthenticationService authService)
            : base(authService)
        { }

        /// <summary>
        /// Lists experiments the authenticated participant is enrolled in.
        /// </summary>
        [HttpGet]
        public IActionResult ListMyExperiments()
        {
            var username = GetAuthenticatedUsername();
            return Ok(new
            {
                username,
                message = "Participant experiment list endpoint stubbed"
            });
        }

        /// <summary>
        /// Returns an experiment bundle for the participant (protocol sessions + tasks, etc.).
        /// </summary>
        [HttpGet("{experimentId}/bundle")]
        public IActionResult GetBundle(string experimentId, [FromQuery] DateTime? since = null)
        {
            var username = GetAuthenticatedUsername();
            return Ok(new
            {
                username,
                experimentId,
                since,
                message = "Participant experiment bundle endpoint stubbed"
            });
        }
    }
}