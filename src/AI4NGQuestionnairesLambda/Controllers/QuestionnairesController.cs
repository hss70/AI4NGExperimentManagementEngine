using Microsoft.AspNetCore.Mvc;
using AI4NGQuestionnairesLambda.Interfaces;
using AI4NGQuestionnairesLambda.Models;
using System.IdentityModel.Tokens.Jwt;

namespace AI4NGQuestionnairesLambda.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QuestionnairesController : ControllerBase
{
    private readonly IQuestionnaireService _questionnaireService;

    public QuestionnairesController(IQuestionnaireService questionnaireService)
    {
        _questionnaireService = questionnaireService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Questionnaire>>> GetAll()
    {
        var questionnaires = await _questionnaireService.GetAllAsync();
        return Ok(questionnaires);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Questionnaire>> GetById(string id)
    {
        var questionnaire = await _questionnaireService.GetByIdAsync(id);
        if (questionnaire == null)
            return NotFound();
        
        return Ok(questionnaire);
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] CreateQuestionnaireRequest request)
    {
        try
        {
            LogDebug($"Create questionnaire called with: {System.Text.Json.JsonSerializer.Serialize(request)}");
            
            var username = GetUsernameFromJwt();
            LogDebug($"Username: {username}");

            if (!IsResearcher())
                return Forbid("Participants cannot create questionnaires");

            var id = await _questionnaireService.CreateAsync(request, username);
            LogDebug($"Created questionnaire with ID: {id}");
            
            return Ok(new { id });
        }
        catch (UnauthorizedAccessException ex)
        {
            LogDebug($"Unauthorized: {ex.Message}");
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            LogDebug($"Error creating questionnaire: {ex.Message}");
            return StatusCode(500, new { error = "Internal server error", details = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> Update(string id, [FromBody] QuestionnaireData data)
    {
        var username = GetUsernameFromJwt();
        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        if (!IsResearcher())
            return Forbid("Participants cannot update questionnaires");

        await _questionnaireService.UpdateAsync(id, data, username);
        return Ok(new { message = "Questionnaire updated successfully" });
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(string id)
    {
        var username = GetUsernameFromJwt();
        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        if (!IsResearcher())
            return Forbid("Participants cannot delete questionnaires");

        await _questionnaireService.DeleteAsync(id);
        return Ok(new { message = "Questionnaire deleted successfully" });
    }

    [HttpPost("batch")]
    public async Task<ActionResult> CreateBatch([FromBody] List<CreateQuestionnaireRequest> requests)
    {
        var username = GetUsernameFromJwt();
        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        if (!IsResearcher())
            return Forbid("Participants cannot create questionnaires");

        var result = await _questionnaireService.CreateBatchAsync(requests, username);
        return Ok(result);
    }

    private string GetUsernameFromJwt()
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

        var token = authHeader.Substring(7);
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            var username = jwtToken.Claims.FirstOrDefault(c => c.Type == "username" || c.Type == "cognito:username")?.Value;
            
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
            Console.WriteLine($"[DEBUG] QuestionnairesController: {message}");
        }
    }

    private bool IsResearcher()
    {
        return Request.Path.StartsWithSegments("/api/researcher");
    }
}