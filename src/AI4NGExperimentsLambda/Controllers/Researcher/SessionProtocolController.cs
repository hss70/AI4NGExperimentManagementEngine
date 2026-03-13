using Microsoft.AspNetCore.Mvc;
using AI4NGExperimentsLambda.Models;
using AI4NGExperimentManagement.Shared;
using AI4NGExperimentsLambda.Interfaces.Researcher;
using System.Threading.Tasks;
using AI4NGExperimentsLambda.Models.Requests;

namespace AI4NGExperimentsLambda.Controllers.Researcher
{
    /// <summary>
    /// Researcher endpoints for defining an experiment's protocol sessions (FIRST/DAILY/WEEKLY).
    /// </summary>
    [Route("api/experiments/{experimentId}/protocol-sessions")]
    public class SessionProtocolController : BaseApiController
    {
        private readonly ISessionProtocolService _sessionProtocolService;

        public SessionProtocolController(ISessionProtocolService sessionProtocolService, IAuthenticationService authService)
        : base(authService)
        {
            _sessionProtocolService = sessionProtocolService;
        }

        /// <summary>
        /// Lists protocol sessions for the experiment (researcher-only).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> List(string experimentId)
        {
            RequireResearcher();
            var sessionProtocols = await _sessionProtocolService.GetProtocolSessionsAsync(experimentId);

            return Ok(sessionProtocols);
        }

        /// <summary>
        /// Gets a single protocol session definition (researcher-only).
        /// </summary>
        [HttpGet("{protocolSessionKey}")]
        public async Task<IActionResult> Get(string experimentId, string protocolSessionKey)
        {
            RequireResearcher();
            var sessionProtocol = await _sessionProtocolService.GetProtocolSessionAsync(experimentId, protocolSessionKey);
            return sessionProtocol == null ? NotFound("Session protocol not found") : Ok(sessionProtocol);
        }


        /// <summary>
        /// Creates a protocol session definition (researcher-only, idempotent).
        /// </summary>
        /// 
        [HttpPost("{protocolSessionKey}")]
        public async Task<IActionResult> Create(string experimentId, string protocolSessionKey, [FromBody] UpsertProtocolSessionRequest request)
        {
            RequireResearcher();
            var created = await _sessionProtocolService.CreateProtocolSessionAsync(experimentId, protocolSessionKey, request, GetAuthenticatedUsername());
            return CreatedAtAction(nameof(Get), new { experimentId, protocolSessionKey = created.ProtocolKey }, created);
        }

        /// <summary>
        /// Creates or replaces a protocol session definition (researcher-only, idempotent).
        /// </summary>
        [HttpPut("{protocolSessionKey}")]
        public async Task<IActionResult> Update(string experimentId, string protocolSessionKey, [FromBody] UpsertProtocolSessionRequest request)
        {
            RequireResearcher();
            var updated = await _sessionProtocolService.UpdateProtocolSessionAsync(experimentId, protocolSessionKey, request, GetAuthenticatedUsername());
            return Ok(updated);
        }

        /// <summary>
        /// Deletes a protocol session definition (researcher-only).
        /// </summary>
        [HttpDelete("{protocolSessionKey}")]
        public async Task<IActionResult> Delete(string experimentId, string protocolSessionKey)
        {
            RequireResearcher();
            await _sessionProtocolService.DeleteProtocolSessionAsync(experimentId, protocolSessionKey, GetAuthenticatedUsername());
            return Ok();
        }
    }
}