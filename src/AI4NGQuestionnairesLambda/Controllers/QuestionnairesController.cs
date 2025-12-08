using Microsoft.AspNetCore.Mvc;
using AI4NGQuestionnairesLambda.Interfaces;
using AI4NGQuestionnairesLambda.Models;
using AI4NGExperimentManagement.Shared;

namespace AI4NGQuestionnairesLambda.Controllers;

[Route("api/[controller]")]
public class QuestionnairesController : BaseApiController
{
    private readonly IQuestionnaireService _questionnaireService;

    public QuestionnairesController(IQuestionnaireService questionnaireService, IAuthenticationService authService)
        : base(authService)
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
            return NotFound("Questionnaire not found");

        return Ok(questionnaire);
    }

    [HttpPost("by-ids")]
    public async Task<ActionResult> GetByIds([FromBody] string[] ids)
    {
        try
        {
            if (ids == null || ids.Length == 0)
                return BadRequest("Provide at least one questionnaire id.");

            var uniqueIds = ids
                .Where(i => !string.IsNullOrWhiteSpace(i))
                .Select(i => i.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (uniqueIds.Count == 0)
                return BadRequest("Provide at least one valid questionnaire id.");

            var result = await _questionnaireService.GetByIdsAsync(uniqueIds);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error fetching questionnaires: {ex.Message}");
        }
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] CreateQuestionnaireRequest request)
    {
        try
        {
            var username = GetAuthenticatedUsername();
            var researcherCheck = RequireResearcher();
            if (researcherCheck != null) return researcherCheck;

            var id = await _questionnaireService.CreateAsync(request, username);
            return Ok(new { id });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
        {
            return Conflict(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return HandleException(ex, "creating questionnaire");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> Update(string id, [FromBody] CreateQuestionnaireRequest request)
    {
        try
        {
            var username = GetAuthenticatedUsername();
            var researcherCheck = RequireResearcher();
            if (researcherCheck != null) return researcherCheck;

            if (request == null || request.Data == null)
                return BadRequest("Invalid questionnaire update request.");

            if (string.IsNullOrWhiteSpace(id))
                return BadRequest("Questionnaire ID cannot be empty.");

            await _questionnaireService.UpdateAsync(id, request.Data, username);

            return Ok(new { message = $"Questionnaire '{id}' updated successfully." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return HandleException(ex, $"Error updating questionnaire {id}");
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(string id)
    {
        try
        {
            var username = GetAuthenticatedUsername();
            var researcherCheck = RequireResearcher();
            if (researcherCheck != null) return researcherCheck;

            await _questionnaireService.DeleteAsync(id, username);
            return Ok(new { message = "Questionnaire deleted successfully" });
        }
        catch (Exception ex)
        {
            return HandleException(ex, "deleting questionnaire");
        }
    }

    [HttpPost("batch")]
    public async Task<ActionResult> CreateBatch([FromBody] List<CreateQuestionnaireRequest> requests)
    {
        try
        {
            var username = GetAuthenticatedUsername();
            var researcherCheck = RequireResearcher();
            if (researcherCheck != null) return researcherCheck;

            var result = await _questionnaireService.CreateBatchAsync(requests, username);

            if (result.Summary.Failed == 0)
                return Ok(result);
            if (result.Summary.Successful == 0)
                return BadRequest(result);
            return StatusCode(207, result); // Partial success
        }
        catch (Exception ex)
        {
            return HandleException(ex, "creating batch questionnaires");
        }
    }
}