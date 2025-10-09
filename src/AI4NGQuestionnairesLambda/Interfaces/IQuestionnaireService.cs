using AI4NGQuestionnairesLambda.Models;

namespace AI4NGQuestionnairesLambda.Interfaces;

public interface IQuestionnaireService
{
    Task<IEnumerable<Questionnaire>> GetAllAsync();
    Task<Questionnaire?> GetByIdAsync(string id);
    Task<string> CreateAsync(CreateQuestionnaireRequest request, string username);
    Task UpdateAsync(string id, QuestionnaireData data, string username);
    Task DeleteAsync(string id);
    Task<Dictionary<string, object>> CreateBatchAsync(List<CreateQuestionnaireRequest> requests, string username);
}