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
        [HttpGet("sync")]
        public async Task<IActionResult> Sync([FromQuery] DateTime? lastSyncTime = null)
        {
            try
            {
                var username = GetAuthenticatedUsername();
                var result = await _experimentService.SyncExperimentAsync(null, lastSyncTime, username);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return HandleException(ex, "syncing experiments");
            }
        }

        /// <summary>
        /// Retrieves all members of a given experiment (researcher-only).
        /// </summary>
        [HttpGet("{experimentId}/members")]
        public async Task<IActionResult> GetMembers(string experimentId)
        {
            try
            {
                RequireResearcher();
                var members = await _experimentService.GetExperimentMembersAsync(experimentId);
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
        [HttpPut("{experimentId}/members/{userSub}")]
        public async Task<IActionResult> AddMember(string experimentId, string userSub, [FromBody] MemberRequest memberData)
        {
            try
            {
                RequireResearcher();
                var username = GetAuthenticatedUsername();
                await _experimentService.AddMemberAsync(experimentId, userSub, memberData, username);
                return Ok(new { message = "Member added successfully" });
            }
            catch (Exception ex)
            {
                return HandleException(ex, "adding experiment member");
            }
        }

        /// <summary>
        /// Removes a member from an experiment (researcher-only).
        /// </summary>
        [HttpDelete("{experimentId}/members/{userSub}")]
        public async Task<IActionResult> RemoveMember(string experimentId, string userSub)
        {
            try
            {
                RequireResearcher();
                var username = GetAuthenticatedUsername();
                await _experimentService.RemoveMemberAsync(experimentId, userSub, username);
                return Ok(new { message = "Member removed successfully" });
            }
            catch (Exception ex)
            {
                return HandleException(ex, "removing experiment member");
            }
        }
    }
}
