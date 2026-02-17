using AI4NGExperimentsLambda.Models;

namespace AI4NGExperimentsLambda.Interfaces;

public interface ITaskService
{
    Task<IEnumerable<object>> GetTasksAsync();
    Task<object?> GetTaskAsync(string taskKey);
    Task<object> CreateTaskAsync(CreateTaskRequest request, string username);
    Task UpdateTaskAsync(string taskKey, TaskData data, string username);
    Task DeleteTaskAsync(string taskKey, string username);
}