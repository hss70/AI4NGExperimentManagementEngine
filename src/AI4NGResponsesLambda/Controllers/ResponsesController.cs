using Microsoft.AspNetCore.Mvc;
using AI4NGResponsesLambda.Interfaces;
using AI4NGResponsesLambda.Models;
using AI4NGExperimentManagement.Shared;

namespace AI4NGResponsesLambda.Controllers;

[Route("api")]
public class ResponsesController : BaseApiController
{
    private readonly IResponseService _responseService;

    public ResponsesController(IResponseService responseService, IAuthenticationService authService)
        : base(authService)
    {
        _responseService = responseService;
    }

    [HttpGet("responses")]
    public async Task<IActionResult> GetResponses([FromQuery] string? experimentId = null, [FromQuery] string? sessionId = null)
    {
        var responses = await _responseService.GetResponsesAsync(experimentId, sessionId);
        return Ok(responses);
    }

    [HttpGet("responses/id/{responseId}")]
    public async Task<IActionResult> GetResponse(string responseId)
    {
        var response = await _responseService.GetResponseAsync(responseId);
        return response == null ? NotFound("Response not found") : Ok(response);
    }

    [HttpPost("responses")]
    public async Task<IActionResult> CreateResponse([FromBody] Response response)
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

    [HttpPut("responses/id/{responseId}")]
    public async Task<IActionResult> UpdateResponse(string responseId, [FromBody] ResponseData data)
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

    [HttpDelete("responses/id/{responseId}")]
    public async Task<IActionResult> DeleteResponse(string responseId)
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


}