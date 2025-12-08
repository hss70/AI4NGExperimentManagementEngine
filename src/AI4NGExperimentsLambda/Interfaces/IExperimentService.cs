using AI4NGExperimentsLambda.Controllers;
using AI4NGExperimentsLambda.Models;

namespace AI4NGExperimentsLambda.Interfaces;

public interface IExperimentService
{
    // Experiment management
    Task<IEnumerable<object>> GetExperimentsAsync();
    Task<object?> GetExperimentAsync(string? experimentId);
    Task<IEnumerable<object>> GetMyExperimentsAsync(string username);
    Task<object> ValidateExperimentAsync(Experiment experiment);
    Task<object> CreateExperimentAsync(Experiment experiment, string username);
    // Create experiment with optional initial sessions seeded from experiment.InitialSessions
    Task UpdateExperimentAsync(string experimentId, ExperimentData data, string username);
    Task DeleteExperimentAsync(string experimentId, string username);
    Task<object> SyncExperimentAsync(string? experimentId, DateTime? lastSyncTime, string username);

    // Member management
    Task<IEnumerable<object>> GetExperimentMembersAsync(string experimentId, string? cohort = null, string? status = null, string? role = null);
    Task AddMemberAsync(string experimentId, string participantUsername, MemberRequest memberData, string username);
    Task AddMembersAsync(string experimentId, IEnumerable<MemberBatchItem> members, string username);
    Task RemoveMemberAsync(string experimentId, string participantUsername, string username);

    // Session management
    Task<IEnumerable<object>> GetExperimentSessionsAsync(string experimentId);
    Task<object?> GetSessionAsync(string experimentId, string sessionId);
    Task<object> CreateSessionAsync(string experimentId, CreateSessionRequest request, string username);
    Task AddSessionsAsync(string experimentId, IEnumerable<CreateSessionRequest> sessions, string username);
    Task UpdateSessionAsync(string experimentId, string sessionId, SessionData data, string username);
    Task DeleteSessionAsync(string experimentId, string sessionId, string username);


}