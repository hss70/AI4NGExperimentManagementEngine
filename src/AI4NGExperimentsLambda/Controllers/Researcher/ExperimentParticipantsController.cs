using Microsoft.AspNetCore.Mvc;
using AI4NGExperimentManagement.Shared;

namespace AI4NGExperimentsLambda.Controllers.Researcher
{
    /// <summary>
    /// Researcher endpoints for enrolling and managing participants in an experiment.
    /// </summary>
    [Route("api/experiments/{experimentId}/participants")]
    public class ExperimentParticipantsController : BaseApiController
    {
        public ExperimentParticipantsController(IAuthenticationService authService)
            : base(authService)
        { }

        /// <summary>
        /// Lists participants enrolled in an experiment (researcher-only).
        /// </summary>
        [HttpGet]
        public IActionResult List(
            string experimentId,
            [FromQuery] string? cohort = null,
            [FromQuery] string? status = null,
            [FromQuery] string? role = null)
        {
            RequireResearcher();
            return Ok(new
            {
                experimentId,
                filters = new { cohort, status, role },
                message = "Participant listing endpoint stubbed"
            });
        }

        /// <summary>
        /// Gets a single participant enrolment record (researcher-only).
        /// </summary>
        [HttpGet("{participantId}")]
        public IActionResult Get(string experimentId, string participantId)
        {
            RequireResearcher();
            return Ok(new
            {
                experimentId,
                participantId,
                message = "Participant get endpoint stubbed"
            });
        }

        /// <summary>
        /// Adds or updates a participant enrolment (researcher-only).
        /// </summary>
        [HttpPut("{participantId}")]
        public IActionResult Upsert(string experimentId, string participantId, [FromBody] object request)
        {
            RequireResearcher();
            return Ok(new
            {
                experimentId,
                participantId,
                received = request,
                message = "Participant upsert endpoint stubbed"
            });
        }

        /// <summary>
        /// Adds multiple participants (researcher-only).
        /// </summary>
        [HttpPost("batch")]
        public IActionResult BatchUpsert(string experimentId, [FromBody] object request)
        {
            RequireResearcher();
            return Ok(new
            {
                experimentId,
                received = request,
                message = "Participant batch upsert endpoint stubbed"
            });
        }

        /// <summary>
        /// Removes a participant from an experiment (researcher-only).
        /// </summary>
        [HttpDelete("{participantId}")]
        public IActionResult Remove(string experimentId, string participantId)
        {
            RequireResearcher();
            return Ok(new
            {
                experimentId,
                participantId,
                message = "Participant remove endpoint stubbed"
            });
        }
    }
}