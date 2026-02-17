using AI4NG.ExperimentManagement.Contracts.Questionnaires;

namespace AI4NGQuestionnairesLambda.Interfaces;

public interface IQuestionnaireService
{
    Task<IEnumerable<QuestionnaireDto>> GetAllAsync(CancellationToken ct = default);
    Task<QuestionnaireDto?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<IEnumerable<QuestionnaireDto>> GetByIdsAsync(List<string> ids, CancellationToken ct = default);

    Task<string> CreateAsync(string id, QuestionnaireDataDto data, string username, CancellationToken ct = default);
    Task UpdateAsync(string id, QuestionnaireDataDto data, string username, CancellationToken ct = default);
    Task DeleteAsync(string id, string username, CancellationToken ct = default);

    Task<BatchResult> CreateBatchAsync(List<CreateQuestionnaireRequest> requests, string username, CancellationToken ct = default);
}
