using Amazon.DynamoDBv2.Model;
using AI4NGExperimentManagement.Shared;
using AI4NGExperimentsLambda.Models;
using AI4NGExperimentsLambda.Models.Constants;
using System.Text.Json;

namespace AI4NGExperimentsLambda.Mappers;

public static class TaskResponseItemMapper
{
    public static string BuildPk(
        string experimentId,
        string participantId,
        string occurrenceKey,
        int taskIndex)
        => $"{DynamoTableKeys.TaskResponsePkPrefix}{experimentId}#{participantId}#{occurrenceKey}#{taskIndex:D4}";

    public static string BuildSk() => DynamoTableKeys.MetadataSk;

    public static Dictionary<string, AttributeValue> MapToItem(TaskResponse response)
    {
        return new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = BuildPk(response.ExperimentId, response.ParticipantId, response.OccurrenceKey, response.TaskIndex) },
            ["SK"] = new AttributeValue { S = BuildSk() },
            ["type"] = new AttributeValue { S = "TaskResponse" },
            ["data"] = new AttributeValue { M = MapDataToAttributeMap(response) },
            ["createdAt"] = new AttributeValue { S = response.CreatedAt },
            ["createdBy"] = new AttributeValue { S = response.ParticipantId }
        };
    }

    public static TaskResponse? MapFromItem(Dictionary<string, AttributeValue>? item)
    {
        if (item == null || item.Count == 0)
            return null;

        if (!item.TryGetValue("data", out var dataAttr) || dataAttr.M == null)
            return null;

        var map = dataAttr.M;

        return new TaskResponse
        {
            ResponseId = map.GetValueOrDefault("ResponseId")?.S ?? string.Empty,
            ExperimentId = map.GetValueOrDefault("ExperimentId")?.S ?? string.Empty,
            ParticipantId = map.GetValueOrDefault("ParticipantId")?.S ?? string.Empty,
            OccurrenceKey = map.GetValueOrDefault("OccurrenceKey")?.S ?? string.Empty,
            TaskIndex = TryGetNullableInt(map, "TaskIndex") ?? 0,
            TaskKey = map.GetValueOrDefault("TaskKey")?.S ?? string.Empty,
            QuestionnaireId = NormalizeOptional(map.GetValueOrDefault("QuestionnaireId")?.S),
            Payload = DynamoDBHelper.ConvertAttributeValueToObject(map.GetValueOrDefault("Payload")) ?? new object(),
            ClientSubmissionId = map.GetValueOrDefault("ClientSubmissionId")?.S ?? string.Empty,
            ClientSubmittedAt = NormalizeOptional(map.GetValueOrDefault("ClientSubmittedAt")?.S),
            SubmittedAt = map.GetValueOrDefault("SubmittedAt")?.S ?? string.Empty,
            CreatedAt = map.GetValueOrDefault("CreatedAt")?.S ?? string.Empty
        };
    }

    private static Dictionary<string, AttributeValue> MapDataToAttributeMap(TaskResponse response)
    {
        var map = new Dictionary<string, AttributeValue>
        {
            ["ResponseId"] = new AttributeValue { S = response.ResponseId },
            ["ExperimentId"] = new AttributeValue { S = response.ExperimentId },
            ["ParticipantId"] = new AttributeValue { S = response.ParticipantId },
            ["OccurrenceKey"] = new AttributeValue { S = response.OccurrenceKey },
            ["TaskIndex"] = new AttributeValue { N = response.TaskIndex.ToString() },
            ["TaskKey"] = new AttributeValue { S = response.TaskKey },
            ["Payload"] = new AttributeValue
            {
                M = DynamoDBHelper.JsonToAttributeValue(JsonSerializer.SerializeToElement(response.Payload))
            },
            ["ClientSubmissionId"] = new AttributeValue { S = response.ClientSubmissionId },
            ["SubmittedAt"] = new AttributeValue { S = response.SubmittedAt },
            ["CreatedAt"] = new AttributeValue { S = response.CreatedAt }
        };

        if (!string.IsNullOrWhiteSpace(response.QuestionnaireId))
            map["QuestionnaireId"] = new AttributeValue { S = response.QuestionnaireId };

        if (!string.IsNullOrWhiteSpace(response.ClientSubmittedAt))
            map["ClientSubmittedAt"] = new AttributeValue { S = response.ClientSubmittedAt };

        return map;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int? TryGetNullableInt(Dictionary<string, AttributeValue> map, string key)
    {
        return map.TryGetValue(key, out var value) &&
               value != null &&
               int.TryParse(value.N, out var parsed)
            ? parsed
            : null;
    }
}
