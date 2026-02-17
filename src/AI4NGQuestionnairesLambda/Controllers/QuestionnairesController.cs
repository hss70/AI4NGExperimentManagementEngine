using Microsoft.AspNetCore.Mvc;
using AI4NGQuestionnairesLambda.Interfaces;
using AI4NG.ExperimentManagement.Contracts.Questionnaires;
using AI4NGExperimentManagement.Shared;

namespace AI4NGQuestionnairesLambda.Controllers;

[Route("api/[controller]")]
public class QuestionnairesController : BaseApiController
{
    private readonly IQuestionnaireService _questionnaireService;

    public QuestionnairesController(
        IQuestionnaireService questionnaireService,
        IAuthenticationService authService)
        : base(authService)
    {
        _questionnaireService = questionnaireService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<QuestionnaireDto>>> GetAll(CancellationToken ct)
    {
        var questionnaires = await _questionnaireService.GetAllAsync(ct);
        return Ok(questionnaires);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<QuestionnaireDto>> GetById(string id, CancellationToken ct)
    {
        var questionnaire = await _questionnaireService.GetByIdAsync(id, ct);
        if (questionnaire == null)
            return NotFound("Questionnaire not found");

        return Ok(questionnaire);
    }

    [HttpPost("by-ids")]
    public async Task<ActionResult> GetByIds([FromBody] string[] ids, CancellationToken ct)
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

        var result = await _questionnaireService.GetByIdsAsync(uniqueIds, ct);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult> Create(
        [FromBody] CreateQuestionnaireRequest request,
        CancellationToken ct)
    {
        var username = GetAuthenticatedUsername();
        var researcherCheck = RequireResearcher();
        if (researcherCheck != null) return researcherCheck;

        var id = await _questionnaireService.CreateAsync(
            request.Id,
            request.Data,
            username,
            ct);

        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> Update(
        string id,
        [FromBody] UpdateQuestionnaireRequest request,
        CancellationToken ct)
    {
        var username = GetAuthenticatedUsername();
        var researcherCheck = RequireResearcher();
        if (researcherCheck != null) return researcherCheck;

        if (request == null || request.Data == null)
            return BadRequest("Invalid questionnaire update request.");

        if (string.IsNullOrWhiteSpace(id))
            return BadRequest("Questionnaire ID cannot be empty.");

        await _questionnaireService.UpdateAsync(id, request.Data, username, ct);

        return Ok(new { message = $"Questionnaire '{id}' updated successfully." });
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(string id, CancellationToken ct)
    {
        var username = GetAuthenticatedUsername();
        var researcherCheck = RequireResearcher();
        if (researcherCheck != null) return researcherCheck;

        await _questionnaireService.DeleteAsync(id, username, ct);

        return Ok(new { message = "Questionnaire deleted successfully" });
    }

    [HttpPost("batch")]
    public async Task<ActionResult> CreateBatch(
        [FromBody] List<CreateQuestionnaireRequest> requests,
        CancellationToken ct)
    {
        var username = GetAuthenticatedUsername();
        var researcherCheck = RequireResearcher();
        if (researcherCheck != null) return researcherCheck;

        if (requests == null || requests.Count == 0)
            return BadRequest(new { error = "No questionnaires provided for batch import." });

        var result = await _questionnaireService.CreateBatchAsync(requests, username, ct);

        if (result.Summary.Failed == 0)
            return Ok(result);

        if (result.Summary.Successful == 0)
            return BadRequest(result);

        return StatusCode(207, result); // Partial success
    }
}
