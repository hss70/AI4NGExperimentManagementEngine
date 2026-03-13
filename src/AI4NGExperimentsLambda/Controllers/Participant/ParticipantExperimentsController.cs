using Microsoft.AspNetCore.Mvc;
using AI4NGExperimentManagement.Shared;
using AI4NGExperimentsLambda.Interfaces.Participant;
using AI4NGExperimentsLambda.Models.Dtos;
using Microsoft.AspNetCore.Http;

namespace AI4NGExperimentsLambda.Controllers.Participant
{
    /// <summary>
    /// Participant endpoints for viewing enrolled experiments and fetching an experiment bundle.
    /// </summary>
    [ApiController]
    [Route("api/me/experiments")]
    public class ParticipantExperimentsController : BaseApiController
    {
        private readonly IParticipantExperimentsService _participantExperimentsService;

        public ParticipantExperimentsController(
            IParticipantExperimentsService participantExperimentsService,
            IAuthenticationService authService)
            : base(authService)
        {
            _participantExperimentsService = participantExperimentsService;
        }

        /// <summary>
        /// Lists experiments the authenticated participant is enrolled in.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<ExperimentListDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ListMyExperiments(CancellationToken ct = default)
        {
            var participantId = GetAuthenticatedUserSub();
            var result = await _participantExperimentsService.GetMyExperimentsAsync(participantId, ct);
            return Ok(result);
        }

        /// <summary>
        /// Returns an experiment bundle for the participant (protocol sessions + tasks, etc.).
        /// </summary>
        [HttpGet("{experimentId}/bundle")]
        [ProducesResponseType(typeof(ExperimentSyncDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetBundle(
            string experimentId,
            [FromQuery] DateTime? since = null,
            CancellationToken ct = default)
        {
            var participantId = GetAuthenticatedUserSub();
            var result = await _participantExperimentsService.GetExperimentBundleAsync(experimentId, participantId, since, ct);
            return Ok(result);
        }
    }
}