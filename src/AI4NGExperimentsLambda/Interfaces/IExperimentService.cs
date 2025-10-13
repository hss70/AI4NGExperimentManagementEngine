using AI4NGExperimentsLambda.Models;

namespace AI4NGExperimentsLambda.Interfaces;

public interface IExperimentService
{
    Task<IEnumerable<object>> GetExperimentsAsync();
    Task<object?> GetExperimentAsync(string experimentId);
    Task<IEnumerable<object>> GetMyExperimentsAsync(string username);
    Task<object> CreateExperimentAsync(Experiment experiment, string username);
    Task UpdateExperimentAsync(string experimentId, ExperimentData data, string username);
    Task DeleteExperimentAsync(string experimentId, string username);
    Task<object> SyncExperimentAsync(string experimentId, DateTime? lastSyncTime, string username);
    Task<IEnumerable<object>> GetExperimentMembersAsync(string experimentId);
    Task AddMemberAsync(string experimentId, string userSub, MemberRequest memberData, string username);
    Task RemoveMemberAsync(string experimentId, string userSub, string username);
}