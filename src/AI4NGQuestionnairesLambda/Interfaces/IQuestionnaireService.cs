using AI4NG.ExperimentManagement.Contracts.Questionnaires;

namespace AI4NGQuestionnairesLambda.Interfaces;

public interface IQuestionnaireService
{
    Task<IEnumerable<QuestionnaireDto>> GetAllAsync();
    Task<QuestionnaireDto?> GetByIdAsync(string id);
    Task<string> CreateAsync(string id, QuestionnaireDataDto data, string username);
    Task UpdateAsync(string id, QuestionnaireDataDto data, string username);
    Task DeleteAsync(string id, string username);
    Task<BatchResult> CreateBatchAsync(List<CreateQuestionnaireRequest> requests, string username);
    Task<IEnumerable<QuestionnaireDto>> GetByIdsAsync(List<string> ids);
}