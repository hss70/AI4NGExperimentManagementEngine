using AI4NGExperimentsLambda.Controllers;
using AI4NGExperimentsLambda.Models;

namespace AI4NGExperimentsLambda.Interfaces;

public interface IExperimentService
{
    // Experiment management
    Task<IEnumerable<object>> GetExperimentsAsync();
    Task<object?> GetExperimentAsync(string? experimentId);
    Task<IEnumerable<object>> GetMyExperimentsAsync(string username);
    Task<object> CreateExperimentAsync(Experiment experiment, string username);
    Task UpdateExperimentAsync(string experimentId, ExperimentData data, string username);
    Task DeleteExperimentAsync(string experimentId, string username);
    Task<object> SyncExperimentAsync(string? experimentId, DateTime? lastSyncTime, string username);

    // Member management
    Task<IEnumerable<object>> GetExperimentMembersAsync(string experimentId);
    Task AddMemberAsync(string experimentId, string userSub, MemberRequest memberData, string username);
    Task RemoveMemberAsync(string experimentId, string userSub, string username);

    // Session management
    Task<IEnumerable<object>> GetExperimentSessionsAsync(string experimentId);
    Task<object?> GetSessionAsync(string experimentId, string sessionId);
    Task<object> CreateSessionAsync(string experimentId, CreateSessionRequest request, string username);
    Task UpdateSessionAsync(string experimentId, string sessionId, SessionData data, string username);
    Task DeleteSessionAsync(string experimentId, string sessionId, string username);


}