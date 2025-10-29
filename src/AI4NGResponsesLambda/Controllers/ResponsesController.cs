using Microsoft.AspNetCore.Mvc;
using AI4NGResponsesLambda.Interfaces;
using AI4NGResponsesLambda.Models;
using AI4NGExperimentManagement.Shared;

namespace AI4NGResponsesLambda.Controllers
{
    /// <summary>
    /// Controller for managing questionnaire and experiment responses.
    /// </summary>
    [Route("api/responses")]
    public class ResponsesController : BaseApiController
    {
        private readonly IResponseService _responseService;

        public ResponsesController(IResponseService responseService, IAuthenticationService authService)
            : base(authService)
        {
            _responseService = responseService;
        }

        /// <summary>
        /// Retrieves all responses for the current user, optionally filtered by experiment or session ID.
        /// </summary>
        /// <param name="experimentId">Optional experiment ID to filter responses.</param>
        /// <param name="sessionId">Optional session ID to filter responses.</param>
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? experimentId = null, [FromQuery] string? sessionId = null)
        {
            try
            {
                var responses = await _responseService.GetResponsesAsync(experimentId, sessionId);
                return Ok(responses);
            }
            catch (Exception ex)
            {
                return HandleException(ex, "retrieving responses");
            }
        }

        /// <summary>
        /// Retrieves a specific response by its ID.
        /// </summary>
        /// <param name="responseId">The unique ID of the response.</param>
        [HttpGet("{responseId}")]
        public async Task<IActionResult> GetById(string responseId)
        {
            try
            {
                var response = await _responseService.GetResponseAsync(responseId);
                return response == null ? NotFound("Response not found") : Ok(response);
            }
            catch (Exception ex)
            {
                return HandleException(ex, "retrieving response");
            }
        }

        /// <summary>
        /// Creates a new response for a questionnaire or experiment session.
        /// </summary>
        /// <param name="response">The response data to create.</param>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Response response)
        {
            try
            {
                var username = GetAuthenticatedUsername();
                var result = await _responseService.CreateResponseAsync(response, username);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return HandleException(ex, "creating response");
            }
        }

        /// <summary>
        /// Updates an existing response.
        /// </summary>
        /// <param name="responseId">The ID of the response to update.</param>
        /// <param name="data">The updated response data.</param>
        [HttpPut("{responseId}")]
        public async Task<IActionResult> Update(string responseId, [FromBody] ResponseData data)
        {
            try
            {
                var username = GetAuthenticatedUsername();
                await _responseService.UpdateResponseAsync(responseId, data, username);
                return Ok(new { message = "Response updated successfully" });
            }
            catch (Exception ex)
            {
                return HandleException(ex, "updating response");
            }
        }

        /// <summary>
        /// Deletes a response (soft delete).
        /// </summary>
        /// <param name="responseId">The ID of the response to delete.</param>
        [HttpDelete("{responseId}")]
        public async Task<IActionResult> Delete(string responseId)
        {
            try
            {
                var username = GetAuthenticatedUsername();
                await _responseService.DeleteResponseAsync(responseId, username);
                return Ok(new { message = "Response deleted successfully" });
            }
            catch (Exception ex)
            {
                return HandleException(ex, "deleting response");
            }
        }

        /// <summary>
        /// Synchronizes responses for the authenticated participant since the last sync time.
        /// </summary>
        /// <param name="lastSyncTime">The last time responses were synced.</param>
        [HttpGet("/api/sync/responses")]
        public async Task<IActionResult> Sync([FromQuery] DateTime? lastSyncTime = null)
        {
            try
            {
                var username = GetAuthenticatedUsername();
                var result = await _responseService.SyncResponsesAsync(lastSyncTime, username);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return HandleException(ex, "syncing responses");
            }
        }
    }
}
