using Microsoft.AspNetCore.Mvc;
using AI4NGResponsesLambda.Interfaces;
using AI4NGResponsesLambda.Models;
using System.IdentityModel.Tokens.Jwt;

namespace AI4NGResponsesLambda.Controllers;

[ApiController]
[Route("api")]
public class ResponsesController : ControllerBase
{
    private readonly IResponseService _responseService;

    public ResponsesController(IResponseService responseService)
    {
        _responseService = responseService;
    }

    [HttpGet("responses")]
    public async Task<IActionResult> GetResponses([FromQuery] string? experimentId = null, [FromQuery] string? sessionId = null)
    {
        var responses = await _responseService.GetResponsesAsync(experimentId, sessionId);
        return Ok(responses);
    }

    [HttpGet("responses/{responseId}")]
    public async Task<IActionResult> GetResponse(string responseId)
    {
        var response = await _responseService.GetResponseAsync(responseId);
        return response == null ? NotFound("Response not found") : Ok(response);
    }

    [HttpPost("responses")]
    public async Task<IActionResult> CreateResponse([FromBody] Response response)
    {
        var username = GetUsernameFromJwt();
        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        var result = await _responseService.CreateResponseAsync(response, username);
        return Ok(result);
    }

    [HttpPut("responses/{responseId}")]
    public async Task<IActionResult> UpdateResponse(string responseId, [FromBody] ResponseData data)
    {
        var username = GetUsernameFromJwt();
        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        await _responseService.UpdateResponseAsync(responseId, data, username);
        return Ok(new { message = "Response updated successfully" });
    }

    [HttpDelete("responses/{responseId}")]
    public async Task<IActionResult> DeleteResponse(string responseId)
    {
        var username = GetUsernameFromJwt();
        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        await _responseService.DeleteResponseAsync(responseId, username);
        return Ok(new { message = "Response deleted successfully" });
    }

    private string? GetUsernameFromJwt()
    {
        LogDebug("Getting username from JWT");
        
        // For local testing, return a test user
        if (Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL") != null)
        {
            LogDebug("Local testing mode - returning testuser");
            return "testuser";
        }

        var authHeader = Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader))
        {
            LogDebug("No Authorization header found");
            throw new UnauthorizedAccessException("Authorization header is required");
        }

        if (!authHeader.StartsWith("Bearer "))
        {
            LogDebug("Invalid Authorization header format");
            throw new UnauthorizedAccessException("Bearer token required");
        }

        var token = authHeader["Bearer ".Length..];
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);
            var username = jwt.Claims.FirstOrDefault(c => c.Type == "cognito:username")?.Value;
            
            if (string.IsNullOrEmpty(username))
            {
                LogDebug("No username claim found in JWT");
                throw new UnauthorizedAccessException("Invalid token: no username claim");
            }
            
            LogDebug($"Successfully extracted username: {username}");
            return username;
        }
        catch (Exception ex) when (!(ex is UnauthorizedAccessException))
        {
            LogDebug($"JWT parsing failed: {ex.Message}");
            throw new UnauthorizedAccessException("Invalid token format");
        }
    }

    private void LogDebug(string message)
    {
        if (Request.Headers.ContainsKey("X-Debug") || Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL") != null)
        {
            Console.WriteLine($"[DEBUG] ResponsesController: {message}");
        }
    }
}