using Microsoft.AspNetCore.Mvc;
using AI4NGExperimentsLambda.Interfaces;
using AI4NGExperimentsLambda.Models;
using AI4NGExperimentManagement.Shared;

namespace AI4NGExperimentsLambda.Controllers;

[Route("api")]
public class ExperimentsController : BaseApiController
{
    private readonly IExperimentService _experimentService;

    public ExperimentsController(IExperimentService experimentService, IAuthenticationService authService)
        : base(authService)
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
        try
        {
            var username = GetAuthenticatedUsername();
            var experiments = await _experimentService.GetMyExperimentsAsync(username);
            return Ok(experiments);
        }
        catch (Exception ex)
        {
            return HandleException(ex, "getting my experiments");
        }
    }

    [HttpPost("researcher/experiments")]
    public async Task<IActionResult> CreateExperiment([FromBody] Experiment experiment)
    {
        try
        {
            var username = GetAuthenticatedUsername();
            var result = await _experimentService.CreateExperimentAsync(experiment, username);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return HandleException(ex, "creating experiment");
        }
    }

    [HttpPut("researcher/experiments/{experimentId}")]
    public async Task<IActionResult> UpdateExperiment(string experimentId, [FromBody] ExperimentData data)
    {
        try
        {
            var username = GetAuthenticatedUsername();
            await _experimentService.UpdateExperimentAsync(experimentId, data, username);
            return Ok(new { message = "Experiment updated successfully" });
        }
        catch (Exception ex)
        {
            return HandleException(ex, "updating experiment");
        }
    }

    [HttpDelete("researcher/experiments/{experimentId}")]
    public async Task<IActionResult> DeleteExperiment(string experimentId)
    {
        try
        {
            var username = GetAuthenticatedUsername();
            await _experimentService.DeleteExperimentAsync(experimentId, username);
            return Ok(new { message = "Experiment deleted successfully" });
        }
        catch (Exception ex)
        {
            return HandleException(ex, "deleting experiment");
        }
    }

    [HttpGet("experiments/{experimentId}/sync")]
    public async Task<IActionResult> SyncExperiment(string experimentId, [FromQuery] DateTime? lastSyncTime = null)
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

    [HttpGet("experiments/{experimentId}/members")]
    public async Task<IActionResult> GetExperimentMembers(string experimentId)
    {
        var members = await _experimentService.GetExperimentMembersAsync(experimentId);
        return Ok(members);
    }

    [HttpPut("experiments/{experimentId}/members/{userSub}")]
    public async Task<IActionResult> AddMember(string experimentId, string userSub, [FromBody] MemberRequest memberData)
    {
        try
        {
            var username = GetAuthenticatedUsername();
            await _experimentService.AddMemberAsync(experimentId, userSub, memberData, username);
            return Ok(new { message = "Member added successfully" });
        }
        catch (Exception ex)
        {
            return HandleException(ex, "adding member");
        }
    }

    [HttpDelete("experiments/{experimentId}/members/{userSub}")]
    public async Task<IActionResult> RemoveMember(string experimentId, string userSub)
    {
        try
        {
            var username = GetAuthenticatedUsername();
            await _experimentService.RemoveMemberAsync(experimentId, userSub, username);
            return Ok(new { message = "Member removed successfully" });
        }
        catch (Exception ex)
        {
            return HandleException(ex, "removing member");
        }
    }


}