using Microsoft.AspNetCore.Mvc;
using AI4NGExperimentManagement.Shared;
using AI4NGExperimentsLambda.Interfaces.Researcher;
using AI4NGExperimentsLambda.Models;
using AI4NGExperimentsLambda.Models.Dtos;
using Microsoft.AspNetCore.Http;

namespace AI4NGExperimentsLambda.Controllers.Researcher
{
    /// <summary>
    /// Researcher endpoints for enrolling and managing participants in an experiment.
    /// </summary>
    [ApiController]
    [Route("api/experiments/{experimentId}/participants")]
    public class ExperimentParticipantsController : BaseApiController
    {
        private readonly IExperimentParticipantsService _experimentParticipantsService;

        public ExperimentParticipantsController(
            IExperimentParticipantsService experimentParticipantsService,
            IAuthenticationService authService)
            : base(authService)
        {
            _experimentParticipantsService = experimentParticipantsService;
        }

        /// <summary>
        /// Lists participants enrolled in an experiment (researcher-only).
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<ExperimentMemberDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> List(
            [FromRoute] string experimentId,
            [FromQuery] string? cohort = null,
            [FromQuery] string? status = null,
            [FromQuery] string? role = null,
            CancellationToken ct = default)
        {
            RequireResearcher();

            var result = await _experimentParticipantsService.GetExperimentParticipantsAsync(
                experimentId,
                cohort,
                status,
                role,
                ct);

            return Ok(result);
        }

        /// <summary>
        /// Gets a single participant enrolment record (researcher-only).
        /// </summary>
        [HttpGet("{participantId}")]
        [ProducesResponseType(typeof(ExperimentMemberDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get(
            [FromRoute] string experimentId,
            [FromRoute] string participantId,
            CancellationToken ct = default)
        {
            RequireResearcher();

            var result = await _experimentParticipantsService.GetExperimentParticipantAsync(
                experimentId,
                participantId,
                ct);

            if (result == null)
                return NotFound();

            return Ok(result);
        }

        /// <summary>
        /// Adds or updates a participant enrolment (researcher-only).
        /// </summary>
        [HttpPut("{participantId}")]
        [ProducesResponseType(typeof(IdResponseDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> Upsert(
            [FromRoute] string experimentId,
            [FromRoute] string participantId,
            [FromBody] ExperimentMemberRequest request,
            CancellationToken ct = default)
        {
            RequireResearcher();

            var username = GetAuthenticatedUsername();

            var result = await _experimentParticipantsService.UpsertParticipantAsync(
                experimentId,
                participantId,
                request,
                username,
                ct);

            return Ok(result);
        }

        /// <summary>
        /// Adds multiple participants (researcher-only).
        /// </summary>
        [HttpPost("batch")]
        [ProducesResponseType(typeof(List<IdResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> BatchUpsert(
            [FromRoute] string experimentId,
            [FromBody] IEnumerable<MemberBatchItem> participants,
            CancellationToken ct = default)
        {
            RequireResearcher();

            var username = GetAuthenticatedUsername();

            var result = await _experimentParticipantsService.UpsertParticipantsBatchAsync(
                experimentId,
                participants,
                username,
                ct);

            return Ok(result);
        }

        /// <summary>
        /// Removes a participant from an experiment (researcher-only).
        /// </summary>
        [HttpDelete("{participantId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Remove(
            [FromRoute] string experimentId,
            [FromRoute] string participantId,
            CancellationToken ct = default)
        {
            RequireResearcher();

            var username = GetAuthenticatedUsername();

            await _experimentParticipantsService.RemoveParticipantAsync(
                experimentId,
                participantId,
                username,
                ct);

            return NoContent();
        }
    }
}