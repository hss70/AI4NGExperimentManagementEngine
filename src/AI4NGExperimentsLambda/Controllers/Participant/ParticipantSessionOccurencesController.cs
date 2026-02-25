using Microsoft.AspNetCore.Mvc;
using AI4NGExperimentManagement.Shared;

namespace AI4NGExperimentsLambda.Controllers.Participant
{
    /// <summary>
    /// Participant endpoints for viewing and executing participant session occurrences.
    /// </summary>
    [Route("api/me/experiments/{experimentId}/occurrences")]
    public class ParticipantSessionOccurrencesController : BaseApiController
    {
        public ParticipantSessionOccurrencesController(IAuthenticationService authService)
            : base(authService)
        { }

        /// <summary>
        /// Lists occurrences for the authenticated participant within a date range.
        /// </summary>
        [HttpGet]
        public IActionResult List(string experimentId, [FromQuery] string? from = null, [FromQuery] string? to = null)
        {
            var username = GetAuthenticatedUsername();
            return Ok(new
            {
                username,
                experimentId,
                from,
                to,
                message = "Participant occurrence list endpoint stubbed"
            });
        }

        /// <summary>
        /// Gets a specific occurrence by key (e.g., FIRST, DAILY#2026-03-05).
        /// </summary>
        [HttpGet("{occurrenceKey}")]
        public IActionResult Get(string experimentId, string occurrenceKey)
        {
            var username = GetAuthenticatedUsername();
            return Ok(new
            {
                username,
                experimentId,
                occurrenceKey,
                message = "Participant occurrence get endpoint stubbed"
            });
        }

        /// <summary>
        /// Marks an occurrence as started (idempotent).
        /// </summary>
        [HttpPost("{occurrenceKey}/start")]
        public IActionResult Start(string experimentId, string occurrenceKey)
        {
            var username = GetAuthenticatedUsername();
            return Ok(new
            {
                username,
                experimentId,
                occurrenceKey,
                message = "Participant occurrence start endpoint stubbed"
            });
        }

        /// <summary>
        /// Marks an occurrence as completed (idempotent).
        /// </summary>
        [HttpPost("{occurrenceKey}/complete")]
        public IActionResult Complete(string experimentId, string occurrenceKey)
        {
            var username = GetAuthenticatedUsername();
            return Ok(new
            {
                username,
                experimentId,
                occurrenceKey,
                message = "Participant occurrence complete endpoint stubbed"
            });
        }
    }
}