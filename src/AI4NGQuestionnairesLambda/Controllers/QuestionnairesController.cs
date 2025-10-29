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
        catch (Exception ex)
        {
            return HandleException(ex, "creating questionnaire");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> Update(string id, [FromBody] QuestionnaireData data)
    {
        try
        {
            var username = GetAuthenticatedUsername();
            var researcherCheck = RequireResearcher();
            if (researcherCheck != null) return researcherCheck;

            await _questionnaireService.UpdateAsync(id, data, username);
            return Ok(new { message = "Questionnaire updated successfully" });
        }
        catch (Exception ex)
        {
            return HandleException(ex, "updating questionnaire");
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