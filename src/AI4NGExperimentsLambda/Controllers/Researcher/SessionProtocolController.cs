using Microsoft.AspNetCore.Mvc;
using AI4NGExperimentManagement.Shared;

namespace AI4NGExperimentsLambda.Controllers.Researcher
{
    /// <summary>
    /// Researcher endpoints for defining an experiment's protocol sessions (FIRST/DAILY/WEEKLY).
    /// </summary>
    [Route("api/experiments/{experimentId}/protocol-sessions")]
    public class SessionProtocolController : BaseApiController
    {
        public SessionProtocolController(IAuthenticationService authService)
            : base(authService)
        { }

        /// <summary>
        /// Lists protocol sessions for the experiment (researcher-only).
        /// </summary>
        [HttpGet]
        public IActionResult List(string experimentId)
        {
            RequireResearcher();
            return Ok(new
            {
                experimentId,
                message = "Protocol session list endpoint stubbed",
                exampleKeys = new[] { "FIRST", "DAILY", "WEEKLY" }
            });
        }

        /// <summary>
        /// Gets a single protocol session definition (researcher-only).
        /// </summary>
        [HttpGet("{protocolSessionKey}")]
        public IActionResult Get(string experimentId, string protocolSessionKey)
        {
            RequireResearcher();
            return Ok(new
            {
                experimentId,
                protocolSessionKey,
                message = "Protocol session get endpoint stubbed"
            });
        }

        /// <summary>
        /// Creates or replaces a protocol session definition (researcher-only, idempotent).
        /// </summary>
        [HttpPut("{protocolSessionKey}")]
        public IActionResult Upsert(string experimentId, string protocolSessionKey, [FromBody] object request)
        {
            RequireResearcher();
            return Ok(new
            {
                experimentId,
                protocolSessionKey,
                received = request,
                message = "Protocol session upsert endpoint stubbed"
            });
        }

        /// <summary>
        /// Deletes a protocol session definition (researcher-only).
        /// </summary>
        [HttpDelete("{protocolSessionKey}")]
        public IActionResult Delete(string experimentId, string protocolSessionKey)
        {
            RequireResearcher();
            return Ok(new
            {
                experimentId,
                protocolSessionKey,
                message = "Protocol session delete endpoint stubbed"
            });
        }
    }
}