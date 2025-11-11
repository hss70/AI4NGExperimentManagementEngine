using AI4NGExperimentsLambda.Models;

namespace AI4NGExperimentsLambda.Interfaces;

public interface ITaskService
{
    Task<IEnumerable<object>> GetTasksAsync();
    Task<object?> GetTaskAsync(string taskId);
    Task<object> CreateTaskAsync(CreateTaskRequest request, string username);
    Task UpdateTaskAsync(string taskId, TaskData data, string username);
    Task DeleteTaskAsync(string taskId, string username);
}