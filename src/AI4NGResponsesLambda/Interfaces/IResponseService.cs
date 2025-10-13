using AI4NGResponsesLambda.Models;

namespace AI4NGResponsesLambda.Interfaces;

public interface IResponseService
{
    Task<IEnumerable<object>> GetResponsesAsync(string? experimentId = null, string? sessionId = null);
    Task<object?> GetResponseAsync(string? responseId);
    Task<object> CreateResponseAsync(Response response, string username);
    Task UpdateResponseAsync(string responseId, ResponseData data, string username);
    Task DeleteResponseAsync(string responseId, string username);
}