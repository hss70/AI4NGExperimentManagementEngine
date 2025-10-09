using Microsoft.AspNetCore.Mvc;
using AI4NGExperimentsLambda.Interfaces;
using AI4NGExperimentsLambda.Models;
using System.IdentityModel.Tokens.Jwt;

namespace AI4NGExperimentsLambda.Controllers;

[ApiController]
[Route("api")]
public class ExperimentsController : ControllerBase
{
    private readonly IExperimentService _experimentService;

    public ExperimentsController(IExperimentService experimentService)
    {
        _experimentService = experimentService;
    }

    [HttpGet("experiments")]
    public async Task<IActionResult> GetExperiments()
    {
        var experiments = await _experimentService.GetExperimentsAsync();
        return Ok(experiments);
    }

    [HttpGet("experiments/{experimentId}")]
    public async Task<IActionResult> GetExperiment(string experimentId)
    {
        var experiment = await _experimentService.GetExperimentAsync(experimentId);
        return experiment == null ? NotFound("Experiment not found") : Ok(experiment);
    }

    [HttpGet("me/experiments")]
    public async Task<IActionResult> GetMyExperiments()
    {
        var username = GetUsernameFromJwt();
        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        var experiments = await _experimentService.GetMyExperimentsAsync(username);
        return Ok(experiments);
    }

    [HttpPost("researcher/experiments")]
    public async Task<IActionResult> CreateExperiment([FromBody] Experiment experiment)
    {
        var username = GetUsernameFromJwt();
        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        var result = await _experimentService.CreateExperimentAsync(experiment, username);
        return Ok(result);
    }

    [HttpPut("researcher/experiments/{experimentId}")]
    public async Task<IActionResult> UpdateExperiment(string experimentId, [FromBody] ExperimentData data)
    {
        var username = GetUsernameFromJwt();
        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        await _experimentService.UpdateExperimentAsync(experimentId, data, username);
        return Ok(new { message = "Experiment updated successfully" });
    }

    [HttpDelete("researcher/experiments/{experimentId}")]
    public async Task<IActionResult> DeleteExperiment(string experimentId)
    {
        var username = GetUsernameFromJwt();
        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        await _experimentService.DeleteExperimentAsync(experimentId, username);
        return Ok(new { message = "Experiment deleted successfully" });
    }

    [HttpPost("experiments/{experimentId}/sync")]
    public async Task<IActionResult> SyncExperiment(string experimentId, [FromBody] SyncRequest syncData)
    {
        var username = GetUsernameFromJwt();
        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        await _experimentService.SyncExperimentAsync(experimentId, syncData, username);
        return Ok(new { message = "Experiment synced successfully" });
    }

    [HttpGet("experiments/{experimentId}/members")]
    public async Task<IActionResult> GetExperimentMembers(string experimentId)
    {
        var members = await _experimentService.GetExperimentMembersAsync(experimentId);
        return Ok(members);
    }

    [HttpPut("experiments/{experimentId}/members/{userSub}")]
    public async Task<IActionResult> AddMember(string experimentId, string userSub, [FromBody] MemberRequest memberData)
    {
        var username = GetUsernameFromJwt();
        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        await _experimentService.AddMemberAsync(experimentId, userSub, memberData, username);
        return Ok(new { message = "Member added successfully" });
    }

    [HttpDelete("experiments/{experimentId}/members/{userSub}")]
    public async Task<IActionResult> RemoveMember(string experimentId, string userSub)
    {
        var username = GetUsernameFromJwt();
        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        await _experimentService.RemoveMemberAsync(experimentId, userSub, username);
        return Ok(new { message = "Member removed successfully" });
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
            Console.WriteLine($"[DEBUG] ExperimentsController: {message}");
        }
    }
}