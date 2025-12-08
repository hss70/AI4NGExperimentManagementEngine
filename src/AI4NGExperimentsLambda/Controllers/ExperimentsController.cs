using Microsoft.AspNetCore.Mvc;
using AI4NGExperimentsLambda.Interfaces;
using AI4NGExperimentsLambda.Models;
using AI4NGExperimentManagement.Shared;

namespace AI4NGExperimentsLambda.Controllers
{
    /// <summary>
    /// Controller for managing experiments, including researcher and participant access.
    /// </summary>
    [Route("api/experiments")]
    public class ExperimentsController : BaseApiController
    {
        private readonly IExperimentService _experimentService;

        public ExperimentsController(IExperimentService experimentService, IAuthenticationService authService)
            : base(authService)
        {
            _experimentService = experimentService;
        }

        /// <summary>
        /// Retrieves all experiments (researcher-only).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                RequireResearcher();
                var experiments = await _experimentService.GetExperimentsAsync();
                return Ok(experiments);
            }
            catch (Exception ex)
            {
                return HandleException(ex, "retrieving experiments");
            }
        }

        /// <summary>
        /// Retrieves a specific experiment by ID (researcher-only).
        /// </summary>
        [HttpGet("{experimentId}")]
        public async Task<IActionResult> GetById(string experimentId)
        {
            try
            {
                RequireResearcher();
                var experiment = await _experimentService.GetExperimentAsync(experimentId);
                return experiment == null ? NotFound("Experiment not found") : Ok(experiment);
            }
            catch (Exception ex)
            {
                return HandleException(ex, "retrieving experiment");
            }
        }

        /// <summary>
        /// Retrieves experiments belonging to the authenticated participant.
        /// </summary>
        [HttpGet("/api/me/experiments")]
        public async Task<IActionResult> GetMyExperiments()
        {
            try
            {
                var username = GetAuthenticatedUsername();
                var experiments = await _experimentService.GetMyExperimentsAsync(username);
                return Ok(experiments);
            }
            catch (Exception ex)
            {
                return HandleException(ex, "retrieving user experiments");
            }
        }

        /// <summary>
        /// Validates experiment dependencies without creating it (researcher-only).
        /// </summary>
        [HttpPost("validate")]
        public async Task<IActionResult> ValidateExperiment([FromBody] Experiment experiment)
        {
            try
            {
                RequireResearcher();
                var result = await _experimentService.ValidateExperimentAsync(experiment);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return HandleException(ex, "validating experiment");
            }
        }

        /// <summary>
        /// Creates a new experiment (researcher-only).
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Experiment experiment)
        {
            try
            {
                RequireResearcher();
                var username = GetAuthenticatedUsername();
                var result = await _experimentService.CreateExperimentAsync(experiment, username);
                return Ok(result);
            }
            catch (ArgumentException ex) when (ex.Message.Contains("Missing questionnaires"))
            {
                var missing = ex.Message.Replace("Missing questionnaires: ", "").Split(", ");
                return BadRequest(new { error = "ValidationError", missingQuestionnaires = missing });
            }
            catch (Exception ex)
            {
                return HandleException(ex, "creating experiment");
            }
        }

        /// <summary>
        /// Updates an existing experiment (researcher-only).
        /// </summary>
        [HttpPut("{experimentId}")]
        public async Task<IActionResult> Update(string experimentId, [FromBody] ExperimentData data)
        {
            try
            {
                RequireResearcher();
                var username = GetAuthenticatedUsername();
                await _experimentService.UpdateExperimentAsync(experimentId, data, username);
                return Ok(new { message = "Experiment updated successfully" });
            }
            catch (Exception ex)
            {
                return HandleException(ex, "updating experiment");
            }
        }

        /// <summary>
        /// Deletes an experiment (researcher-only).
        /// </summary>
        [HttpDelete("{experimentId}")]
        public async Task<IActionResult> Delete(string experimentId)
        {
            try
            {
                RequireResearcher();
                var username = GetAuthenticatedUsername();
                await _experimentService.DeleteExperimentAsync(experimentId, username);
                return Ok(new { message = "Experiment deleted successfully" });
            }
            catch (Exception ex)
            {
                return HandleException(ex, "deleting experiment");
            }
        }

        /// <summary>
        /// Synchronizes experiment data for a participant.
        /// </summary>
        [HttpGet("{experimentId}/sync")]
        public async Task<IActionResult> Sync(string experimentId, [FromQuery] DateTime? lastSyncTime = null)
        {
            try
            {
                var username = GetAuthenticatedUsername();
                var result = await _experimentService.SyncExperimentAsync(experimentId, lastSyncTime, username);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return HandleException(ex, "syncing experiment");
            }
        }

        /// <summary>
        /// Retrieves all members of a given experiment (researcher-only).
        /// </summary>
        [HttpGet("{experimentId}/members")]
        public async Task<IActionResult> GetMembers(string experimentId, [FromQuery] string? cohort = null, [FromQuery] string? status = null, [FromQuery] string? role = null)
        {
            try
            {
                RequireResearcher();
                var members = await _experimentService.GetExperimentMembersAsync(experimentId, cohort, status, role);
                return Ok(members);
            }
            catch (Exception ex)
            {
                return HandleException(ex, "retrieving experiment members");
            }
        }

        /// <summary>
        /// Adds a member to an experiment (researcher-only).
        /// </summary>
        [HttpPut("{experimentId}/members/{participantUsername}")]
        public async Task<IActionResult> AddMember(string experimentId, string participantUsername, [FromBody] MemberRequest memberData)
        {
            try
            {
                RequireResearcher();
                var username = GetAuthenticatedUsername();
                await _experimentService.AddMemberAsync(experimentId, participantUsername, memberData, username);
                return Ok(new { message = "Member added successfully" });
            }
            catch (Exception ex)
            {
                return HandleException(ex, "adding experiment member");
            }
        }

        /// <summary>
        /// Adds multiple members to an experiment (researcher-only).
        /// </summary>
        [HttpPost("{experimentId}/members/batch")]
        public async Task<IActionResult> AddMembersBatch(string experimentId, [FromBody] List<MemberBatchItem> members)
        {
            try
            {
                RequireResearcher();
                var username = GetAuthenticatedUsername();
                await _experimentService.AddMembersAsync(experimentId, members, username);
                return Ok(new { message = "Members added successfully", count = members?.Count ?? 0 });
            }
            catch (Exception ex)
            {
                return HandleException(ex, "adding experiment members batch");
            }
        }

        /// <summary>
        /// Removes a member from an experiment (researcher-only).
        /// </summary>
        [HttpDelete("{experimentId}/members/{participantUsername}")]
        public async Task<IActionResult> RemoveMember(string experimentId, string participantUsername)
        {
            try
            {
                RequireResearcher();
                var username = GetAuthenticatedUsername();
                await _experimentService.RemoveMemberAsync(experimentId, participantUsername, username);
                return Ok(new { message = "Member removed successfully" });
            }
            catch (Exception ex)
            {
                return HandleException(ex, "removing experiment member");
            }
        }

        /// <summary>
        /// Retrieves all sessions for an experiment (researcher-only).
        /// </summary>
        [HttpGet("{experimentId}/sessions")]
        public async Task<IActionResult> GetSessions(string experimentId)
        {
            try
            {
                RequireResearcher();
                var sessions = await _experimentService.GetExperimentSessionsAsync(experimentId);
                return Ok(sessions);
            }
            catch (Exception ex)
            {
                return HandleException(ex, "retrieving experiment sessions");
            }
        }

        /// <summary>
        /// Retrieves a specific session by ID (researcher-only).
        /// </summary>
        [HttpGet("{experimentId}/sessions/{sessionId}")]
        public async Task<IActionResult> GetSession(string experimentId, string sessionId)
        {
            try
            {
                RequireResearcher();
                var session = await _experimentService.GetSessionAsync(experimentId, sessionId);
                return session == null ? NotFound("Session not found") : Ok(session);
            }
            catch (Exception ex)
            {
                return HandleException(ex, "retrieving session");
            }
        }

        /// <summary>
        /// Creates a new session for an experiment (researcher-only).
        /// </summary>
        [HttpPost("{experimentId}/sessions")]
        public async Task<IActionResult> CreateSession(string experimentId, [FromBody] CreateSessionRequest request)
        {
            try
            {
                RequireResearcher();
                request.ExperimentId = experimentId;
                var username = GetAuthenticatedUsername();
                var result = await _experimentService.CreateSessionAsync(experimentId, request, username);
                return Ok(result);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
            {
                return BadRequest(new { error = "ValidationError", message = ex.Message });
            }
            catch (Exception ex)
            {
                return HandleException(ex, "creating session");
            }
        }

        /// <summary>
        /// Adds or updates multiple sessions for an experiment (researcher-only).
        /// </summary>
        [HttpPost("{experimentId}/sessions/batch")]
        public async Task<IActionResult> AddSessionsBatch(string experimentId, [FromBody] List<CreateSessionRequest> sessions)
        {
            try
            {
                RequireResearcher();
                var username = GetAuthenticatedUsername();
                await _experimentService.AddSessionsAsync(experimentId, sessions, username);
                return Ok(new { message = "Sessions upserted successfully", count = sessions?.Count ?? 0 });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
            {
                return BadRequest(new { error = "ValidationError", message = ex.Message });
            }
            catch (Exception ex)
            {
                return HandleException(ex, "adding sessions batch");
            }
        }

        /// <summary>
        /// Updates an existing session (researcher-only).
        /// </summary>
        [HttpPut("{experimentId}/sessions/{sessionId}")]
        public async Task<IActionResult> UpdateSession(string experimentId, string sessionId, [FromBody] SessionData data)
        {
            try
            {
                RequireResearcher();
                var username = GetAuthenticatedUsername();
                await _experimentService.UpdateSessionAsync(experimentId, sessionId, data, username);
                return Ok(new { message = "Session updated successfully" });
            }
            catch (Exception ex)
            {
                return HandleException(ex, "updating session");
            }
        }

        /// <summary>
        /// Deletes a session (researcher-only).
        /// </summary>
        [HttpDelete("{experimentId}/sessions/{sessionId}")]
        public async Task<IActionResult> DeleteSession(string experimentId, string sessionId)
        {
            try
            {
                RequireResearcher();
                var username = GetAuthenticatedUsername();
                await _experimentService.DeleteSessionAsync(experimentId, sessionId, username);
                return Ok(new { message = "Session deleted successfully" });
            }
            catch (Exception ex)
            {
                return HandleException(ex, "deleting session");
            }
        }
    }
}
