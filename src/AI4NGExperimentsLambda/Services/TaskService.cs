using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGExperimentsLambda.Interfaces;
using AI4NGExperimentsLambda.Models;
using AI4NGExperimentManagement.Shared;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AI4NGExperimentsLambda.Services;

public class TaskService : ITaskService
{
    private readonly IAmazonDynamoDB _dynamoClient;
    private readonly string _experimentsTable;

    public TaskService(IAmazonDynamoDB dynamoClient)
    {
        _dynamoClient = dynamoClient;
        _experimentsTable = Environment.GetEnvironmentVariable("EXPERIMENTS_TABLE") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(_experimentsTable))
            throw new InvalidOperationException("EXPERIMENTS_TABLE environment variable is not set");
    }

    public async Task<IEnumerable<object>> GetTasksAsync()
    {
        var response = await _dynamoClient.QueryAsync(new QueryRequest
        {
            TableName = _experimentsTable,
            IndexName = "GSI1",
            KeyConditionExpression = "GSI1PK = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new AttributeValue("TASK"),
                [":false"] = new AttributeValue { BOOL = false }
            },
            // newest first
            ScanIndexForward = false,
            // filter out soft-deleted tasks
            FilterExpression = "attribute_not_exists(IsDeleted) OR IsDeleted = :false"
        });

        return response.Items.Select(item => new
        {
            id = item["PK"].S.Replace("TASK#", ""),
            data = DynamoDBHelper.ConvertAttributeValueToObject(item["data"]),
            createdAt = Utilities.ParseIsoUtcDateTimeOrMin(item.GetValueOrDefault("createdAt")?.S),
            updatedAt = Utilities.ParseIsoUtcDateTimeOrMin(item.GetValueOrDefault("updatedAt")?.S)
        });
    }

    public async Task<object?> GetTaskAsync(string taskKey)
    {
        taskKey = NormaliseTaskKey(taskKey);

        var response = await _dynamoClient.GetItemAsync(new GetItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue($"TASK#{taskKey}"),
                ["SK"] = new AttributeValue("METADATA")
            }
        });

        if (!response.IsItemSet)
            return null;

        if (response.Item.TryGetValue("IsDeleted", out var del) && del.BOOL.HasValue && del.BOOL.Value)
            return null;

        return new
        {
            id = taskKey,
            data = DynamoDBHelper.ConvertAttributeValueToObject(response.Item["data"]),
            createdAt = Utilities.ParseIsoUtcDateTimeOrMin(response.Item.GetValueOrDefault("createdAt")?.S),
            updatedAt = Utilities.ParseIsoUtcDateTimeOrMin(response.Item.GetValueOrDefault("updatedAt")?.S)
        };
    }

    public async Task<object> CreateTaskAsync(CreateTaskRequest request, string username)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (request.Data == null) throw new ArgumentException("Task Data is required.");

        var taskKey = NormaliseTaskKey(request.TaskKey);
        ValidateTaskKey(taskKey);

        var taskData = NormaliseTaskData(request.Data);

        ValidateQuestionnaireConfiguration(taskData);
        await ValidateQuestionnairesExistAsync(taskData.QuestionnaireIds);

        var now = Utilities.GetCurrentTimeStampIso();

        await _dynamoClient.PutItemAsync(new PutItemRequest
        {
            TableName = _experimentsTable,
            ConditionExpression = "attribute_not_exists(PK) AND attribute_not_exists(SK)",
            Item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue($"TASK#{taskKey}"),
                ["SK"] = new AttributeValue("METADATA"),

                // GSI for fast listing + sorting
                ["GSI1PK"] = new AttributeValue("TASK"),
                ["GSI1SK"] = new AttributeValue(now),

                ["EntityType"] = new AttributeValue("Task"),
                ["data"] = new AttributeValue
                {
                    M = DynamoDBHelper.JsonToAttributeValue(JsonSerializer.SerializeToElement(taskData))
                },

                ["IsDeleted"] = new AttributeValue { BOOL = false },
                ["createdBy"] = new AttributeValue(username),
                ["createdAt"] = new AttributeValue(now),
                ["updatedBy"] = new AttributeValue(username),
                ["updatedAt"] = new AttributeValue(now),
                ["Version"] = new AttributeValue { N = "1" }
            }
        });

        return new { id = taskKey };
    }

    public async Task UpdateTaskAsync(string taskKey, TaskData data, string username)
    {
        taskKey = NormaliseTaskKey(taskKey);
        ValidateTaskKey(taskKey);

        data = NormaliseTaskData(data);

        ValidateQuestionnaireConfiguration(data);
        await ValidateQuestionnairesExistAsync(data.QuestionnaireIds);

        var now = Utilities.GetCurrentTimeStampIso();

        await _dynamoClient.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue($"TASK#{taskKey}"),
                ["SK"] = new AttributeValue("METADATA")
            },
            ConditionExpression =
                "attribute_exists(PK) AND attribute_exists(SK) AND (attribute_not_exists(IsDeleted) OR IsDeleted = :false)",
            UpdateExpression =
                "SET #data = :data, updatedAt = :timestamp, updatedBy = :user ADD Version :one",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#data"] = "data"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":data"] = new AttributeValue
                {
                    M = DynamoDBHelper.JsonToAttributeValue(JsonSerializer.SerializeToElement(data))
                },
                [":timestamp"] = new AttributeValue(now),
                [":user"] = new AttributeValue(username),
                [":false"] = new AttributeValue { BOOL = false },
                [":one"] = new AttributeValue { N = "1" }
            }
        });
    }

    public async Task DeleteTaskAsync(string taskKey, string username)
    {
        taskKey = NormaliseTaskKey(taskKey);
        var now = Utilities.GetCurrentTimeStampIso();

        await _dynamoClient.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue($"TASK#{taskKey}"),
                ["SK"] = new AttributeValue("METADATA")
            },
            ConditionExpression = "attribute_exists(PK) AND attribute_exists(SK)",
            UpdateExpression =
                "SET IsDeleted = :true, " +
                "DeletedAt = if_not_exists(DeletedAt, :now), " +
                "updatedAt = :now, updatedBy = :user " +
                "ADD Version :one",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":true"] = new AttributeValue { BOOL = true },
                [":now"] = new AttributeValue(now),
                [":user"] = new AttributeValue(username),
                [":one"] = new AttributeValue { N = "1" }
            }
        });
    }

    private static readonly Regex TaskKeyRegex = new("^[A-Z0-9_]{3,64}$", RegexOptions.Compiled);

    private static string NormaliseTaskKey(string taskKey)
    {
        var normalized = taskKey.Trim().ToUpperInvariant();

        return normalized;
    }

    private static void ValidateTaskKey(string taskKey)
    {
        if (string.IsNullOrWhiteSpace(taskKey))
            throw new ArgumentException("TaskKey is required.");

        if (!TaskKeyRegex.IsMatch(taskKey))
            throw new ArgumentException("Invalid TaskKey format. Use 3-64 chars: A-Z, 0-9, underscore. Example: TRAIN_EEG.");
    }


    private static string NormalizeTaskType(string type)
    {
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Task Type is required.");

        // Keep as-is except trim; enforce canonical casing for known types
        var t = type.Trim();

        // Canonicalize common variants (optional but practical)
        return t switch
        {
            "Neurogame" => "NeuroGame",
            "NeuroGame" => "NeuroGame",
            "Training" => "Training",
            "Questionnaire" => "Questionnaire",
            "QuestionnaireSet" => "QuestionnaireSet",
            _ => t
        };
    }

    private static List<string> NormalizeQuestionnaireIds(IEnumerable<string>? questionnaireIds)
    {
        return questionnaireIds?
            .Where(q => !string.IsNullOrWhiteSpace(q))
            .Select(q => q.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? new List<string>();
    }

    private static TaskData NormaliseTaskData(TaskData task)
    {
        if (task == null) throw new ArgumentNullException(nameof(task));

        task.Type = NormalizeTaskType(task.Type);

        // Canonical questionnaire list (even for single questionnaire tasks)
        task.QuestionnaireIds = NormalizeQuestionnaireIds(task.QuestionnaireIds);

        // Ensure Configuration is never null
        task.Configuration ??= new Dictionary<string, object>();

        // Optional: trim name/description
        task.Name = task.Name?.Trim() ?? string.Empty;
        task.Description = task.Description?.Trim() ?? string.Empty;

        return task;
    }

    private static void ValidateQuestionnaireConfiguration(TaskData task)
    {
        var type = task.Type;
        var questionnaireIds = task.QuestionnaireIds ?? new List<string>();

        switch (type)
        {
            case "Training":
            case "NeuroGame":
                if (questionnaireIds.Any())
                    throw new ArgumentException($"{type} tasks must not define QuestionnaireIds.");
                break;

            case "Questionnaire":
                if (questionnaireIds.Count != 1)
                    throw new ArgumentException("Questionnaire tasks must define exactly one QuestionnaireId in QuestionnaireIds.");
                break;

            case "QuestionnaireSet":
                if (!questionnaireIds.Any())
                    throw new ArgumentException("QuestionnaireSet tasks must define at least one QuestionnaireId in QuestionnaireIds.");
                break;

            default:
                throw new ArgumentException($"Unsupported task Type '{type}'. Supported: Training, NeuroGame, Questionnaire, QuestionnaireSet.");
        }
    }

    private async Task ValidateQuestionnairesExistAsync(IEnumerable<string>? questionnaireIds)
    {
        if (questionnaireIds == null) return;

        var ids = questionnaireIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ids.Count == 0) return;

        var missing = new List<string>();

        foreach (var questionnaireId in ids)
        {
            var exists = await ValidateQuestionnaireExists(questionnaireId);
            if (!exists) missing.Add(questionnaireId);
        }

        if (missing.Count > 0)
            throw new ArgumentException($"Missing questionnaires: {string.Join(", ", missing)}");
    }

    private async Task<bool> ValidateQuestionnaireExists(string questionnaireId)
    {
        try
        {
            var response = await _dynamoClient.GetItemAsync(new GetItemRequest
            {
                TableName = Environment.GetEnvironmentVariable("QUESTIONNAIRES_TABLE") ?? "AI4NGQuestionnaires-dev",
                Key = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new($"QUESTIONNAIRE#{questionnaireId}"),
                    ["SK"] = new("CONFIG")
                }
            });
            return response.IsItemSet;
        }
        catch
        {
            return false;
        }
    }
}