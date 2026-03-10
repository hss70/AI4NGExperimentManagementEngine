using AI4NGExperimentsLambda.Models;
using AI4NGExperimentsLambda.Models.Dtos;

namespace AI4NGExperimentsLambda.Interfaces;

public interface ITaskService
{
    Task<IEnumerable<AI4NGTask>> GetTasksAsync();
    Task<AI4NGTask?> GetTaskAsync(string taskKey);
    Task<IdResponseDto> CreateTaskAsync(CreateTaskRequest request, string username);
    Task UpdateTaskAsync(string taskKey, TaskData data, string username);
    Task DeleteTaskAsync(string taskKey, string username);
}