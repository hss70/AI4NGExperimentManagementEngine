using Amazon.DynamoDBv2.Model;
using AI4NGExperimentsLambda.Models;
using AI4NGExperimentsLambda.Models.Dtos;
using AI4NGExperimentManagement.Shared;
using System.Text.Json;

namespace AI4NGExperimentsLambda.Mappers;

public static class TaskItemMapper
{
    public static AI4NGTask MapItemToTask(Dictionary<string, AttributeValue> item)
    {
        return new AI4NGTask
        {
            TaskKey = item["PK"].S.Replace("TASK#", ""),
            Data = JsonSerializer.Deserialize<TaskData>(
                JsonSerializer.Serialize(DynamoDBHelper.ConvertAttributeValueToObject(item["data"]))
            ) ?? new TaskData(),
            CreatedAt = Utilities.ParseIsoUtcDateTimeOrMin(item.GetValueOrDefault("createdAt")?.S),
            UpdatedAt = Utilities.ParseIsoUtcDateTimeOrMin(item.GetValueOrDefault("updatedAt")?.S),
            CreatedBy = item.GetValueOrDefault("createdBy")?.S ?? string.Empty
        };
    }

    public static TaskDto MapTaskToDto(AI4NGTask task)
    {
        return new TaskDto
        {
            Id = task.TaskKey,
            Data = task.Data ?? new TaskData(),
            CreatedAt = task.CreatedAt == DateTime.MinValue ? null : task.CreatedAt.ToString("O"),
            UpdatedAt = task.UpdatedAt == DateTime.MinValue ? null : task.UpdatedAt.ToString("O"),
            CreatedBy = task.CreatedBy ?? string.Empty
        };
    }
}