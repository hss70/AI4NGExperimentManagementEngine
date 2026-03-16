using Microsoft.AspNetCore.Mvc;
using AI4NGExperimentManagement.Shared;
using AI4NGExperimentsLambda.Interfaces.Participant;
using AI4NGExperimentsLambda.Models.Requests;
using AI4NGExperimentsLambda.Models.Requests.Participant;

namespace AI4NGExperimentsLambda.Controllers.Participant
{
    /// <summary>
    /// Participant endpoints for viewing and executing participant session occurrences.
    /// </summary>
    [Route("api/me/experiments/{experimentId}/occurrences")]
    public class ParticipantSessionOccurrencesController : BaseApiController
    {
        private readonly IParticipantSessionOccurrencesService _occurrencesService;

        public ParticipantSessionOccurrencesController(
            IParticipantSessionOccurrencesService occurrencesService,
            IAuthenticationService authService)
            : base(authService)
        {
            _occurrencesService = occurrencesService;
        }

        /// <summary>
        /// Lists occurrences for the authenticated participant within an optional date range.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> List(
            string experimentId,
            [FromQuery] string? from = null,
            [FromQuery] string? to = null,
            CancellationToken ct = default)
        {
            var participantId = GetAuthenticatedUserSub();

            var occurrences = await _occurrencesService.ListOccurrencesAsync(
                experimentId,
                participantId,
                from,
                to,
                ct);

            return Ok(occurrences);
        }

        /// <summary>
        /// Gets a specific occurrence by key (e.g., FIRST, DAILY#2026-03-05).
        /// </summary>
        [HttpGet("{occurrenceKey}")]
        public async Task<IActionResult> Get(
            string experimentId,
            string occurrenceKey,
            CancellationToken ct = default)
        {
            var participantId = GetAuthenticatedUserSub();

            var occurrence = await _occurrencesService.GetOccurrenceAsync(
                experimentId,
                participantId,
                occurrenceKey,
                ct);

            if (occurrence == null)
                return NotFound(new { error = $"Occurrence '{occurrenceKey}' was not found" });

            return Ok(occurrence);
        }

        /// <summary>
        /// Resolves what the participant should do now.
        /// </summary>
        [HttpGet("current")]
        public async Task<IActionResult> ResolveCurrent(
            string experimentId,
            CancellationToken ct = default)
        {
            var participantId = GetAuthenticatedUserSub();

            var resolved = await _occurrencesService.ResolveCurrentOccurrenceAsync(
                experimentId,
                participantId,
                ct);

            return Ok(resolved);
        }

        /// <summary>
        /// Marks an occurrence as started (idempotent).
        /// </summary>
        [HttpPost("{occurrenceKey}/start")]
        public async Task<IActionResult> Start(
            string experimentId,
            string occurrenceKey,
            [FromBody] StartOccurrenceRequest? request = null,
            CancellationToken ct = default)
        {
            var participantId = GetAuthenticatedUserSub();

            var occurrence = await _occurrencesService.StartOccurrenceAsync(
                experimentId,
                participantId,
                occurrenceKey,
                request,
                ct);

            return Ok(occurrence);
        }

        /// <summary>
        /// Marks an occurrence as completed (idempotent).
        /// </summary>
        [HttpPost("{occurrenceKey}/complete")]
        public async Task<IActionResult> Complete(
            string experimentId,
            string occurrenceKey,
            [FromBody] CompleteOccurrenceRequest? request = null,
            CancellationToken ct = default)
        {
            var participantId = GetAuthenticatedUserSub();

            var occurrence = await _occurrencesService.CompleteOccurrenceAsync(
                experimentId,
                participantId,
                occurrenceKey,
                request,
                ct);

            return Ok(occurrence);
        }

        /// <summary>
        /// Creates a participant-initiated optional occurrence.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create(
            string experimentId,
            [FromBody] CreateOccurrenceRequest request,
            CancellationToken ct = default)
        {
            var participantId = GetAuthenticatedUserSub();

            var occurrence = await _occurrencesService.CreateOccurrenceAsync(
                experimentId,
                participantId,
                request,
                ct);

            return Ok(occurrence);
        }
    }
}