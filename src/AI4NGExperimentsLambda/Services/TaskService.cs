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
        if (string.IsNullOrWhiteSpace(request.TaskKey))
            throw new ArgumentException("TaskKey is required");

        var taskKey = request.TaskKey.Trim().ToUpperInvariant();
        if (!Regex.IsMatch(taskKey, "^[A-Z0-9_]{3,64}$")) throw new ArgumentException("Invalid TaskKey format");

        // Keep your questionnaire validation as-is for now
        if (request.Configuration?.ContainsKey("questionnaireId") == true)
        {
            var questionnaireId = request.Configuration["questionnaireId"]?.ToString();
            if (!string.IsNullOrWhiteSpace(questionnaireId))
            {
                var exists = await ValidateQuestionnaireExists(questionnaireId);
                if (!exists)
                    throw new ArgumentException($"Missing questionnaires: {questionnaireId}");
            }
        }

        var now = Utilities.GetCurrentTimeStampIso();

        var taskData = new TaskData
        {
            Name = request.Name,
            Type = request.Type,
            Description = request.Description,
            Configuration = request.Configuration ?? new(),
            EstimatedDuration = request.EstimatedDuration
        };

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
                [":data"] = new AttributeValue { M = DynamoDBHelper.JsonToAttributeValue(JsonSerializer.SerializeToElement(data)) },
                [":timestamp"] = new AttributeValue(now),
                [":user"] = new AttributeValue(username),
                [":false"] = new AttributeValue { BOOL = false },
                [":one"] = new AttributeValue { N = "1" }
            }
        });
    }

    public async Task DeleteTaskAsync(string taskKey, string username)
    {
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