using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGExperimentsLambda.Interfaces;
using AI4NGExperimentsLambda.Models;
using AI4NGExperimentManagement.Shared;
using AI4NGExperimentsLambda.Models.Dtos;
using System.Text.Json;
using System.Text.RegularExpressions;
using AI4NGExperimentsLambda.Models.Constants;
using AI4NGExperimentsLambda.Mappers;

namespace AI4NGExperimentsLambda.Services.Researcher;

public class TaskService : ITaskService
{
    private readonly IAmazonDynamoDB _dynamoClient;
    private readonly string _experimentsTable;
    private readonly string _questionnairesTable;
    private const int MaxBatchCreateTasks = 25;

    public TaskService(IAmazonDynamoDB dynamoClient)
    {
        _dynamoClient = dynamoClient;
        _experimentsTable = Environment.GetEnvironmentVariable("EXPERIMENTS_TABLE") ?? string.Empty;
        _questionnairesTable = Environment.GetEnvironmentVariable("QUESTIONNAIRES_TABLE") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(_experimentsTable))
            throw new InvalidOperationException("EXPERIMENTS_TABLE environment variable is not set");

        if (string.IsNullOrWhiteSpace(_questionnairesTable))
            throw new InvalidOperationException("QUESTIONNAIRES_TABLE environment variable is not set");
    }

    public async Task<IEnumerable<AI4NGTask>> GetTasksAsync()
    {
        var response = await _dynamoClient.QueryAsync(new QueryRequest
        {
            TableName = _experimentsTable,
            IndexName = "GSI1",
            KeyConditionExpression = "GSI1PK = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new AttributeValue(DynamoTableKeys.TaskGsiPk),
                [":false"] = new AttributeValue { BOOL = false }
            },
            // newest first
            ScanIndexForward = false,
            // filter out soft-deleted tasks
            FilterExpression = "attribute_not_exists(IsDeleted) OR IsDeleted = :false"
        });

        return response.Items.Select(TaskItemMapper.MapItemToTask);
    }

    public async Task<AI4NGTask?> GetTaskAsync(string taskKey)
    {
        taskKey = NormaliseTaskKey(taskKey);

        var response = await _dynamoClient.GetItemAsync(new GetItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue($"{DynamoTableKeys.TaskPkPrefix}{taskKey}"),
                ["SK"] = new AttributeValue(DynamoTableKeys.MetadataSk)
            }
        });

        if (!response.IsItemSet)
            return null;

        if (response.Item.TryGetValue("IsDeleted", out var del) && del.BOOL.HasValue && del.BOOL.Value)
            return null;

        return TaskItemMapper.MapItemToTask(response.Item);
    }
    public async Task<List<IdResponseDto>> CreateTasksBatchAsync(
        IEnumerable<CreateTaskRequest> requests,
        string username,
        CancellationToken ct = default)
    {
        if (requests == null)
            throw new ArgumentException("Task batch payload is required.");

        var items = requests.ToList();

        if (items.Count == 0)
            return new List<IdResponseDto>();

        if (items.Count > MaxBatchCreateTasks)
            throw new ArgumentException($"A maximum of {MaxBatchCreateTasks} tasks can be created in one batch.");

        var results = new List<IdResponseDto>(items.Count);

        foreach (var request in items)
        {
            ct.ThrowIfCancellationRequested();

            var created = await CreateTaskAsync(request, username);
            results.Add(created);
        }

        return results;
    }
    public async Task<IdResponseDto> CreateTaskAsync(CreateTaskRequest request, string username)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (request.Data == null) throw new ArgumentException("Task Data is required.");

        var taskKey = NormaliseTaskKey(request.TaskKey);
        ValidateTaskKey(taskKey);

        var taskData = NormaliseTaskData(request.Data);

        ValidateTaskConfiguration(taskData);
        await ValidateQuestionnairesExistAsync(taskData.QuestionnaireIds);

        var now = Utilities.GetCurrentTimeStampIso();

        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue($"{DynamoTableKeys.TaskPkPrefix}{taskKey}"),
            ["SK"] = new AttributeValue(DynamoTableKeys.MetadataSk),

            // GSI for fast listing + sorting
            ["GSI1PK"] = new AttributeValue(DynamoTableKeys.TaskGsiPk),
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
        };

        await _dynamoClient.PutItemAsync(new PutItemRequest
        {
            TableName = _experimentsTable,

            ConditionExpression =
                    "attribute_not_exists(PK) " +
                    "OR attribute_not_exists(syncMetadata) " +
                    "OR attribute_not_exists(syncMetadata.isDeleted) " +
                    "OR syncMetadata.isDeleted = :true",
            Item = item,
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":true"] = new AttributeValue { BOOL = true }
            }
        });

        return new IdResponseDto { Id = taskKey };
    }

    public async Task UpdateTaskAsync(string taskKey, TaskData data, string username)
    {
        taskKey = NormaliseTaskKey(taskKey);
        ValidateTaskKey(taskKey);

        data = NormaliseTaskData(data);

        ValidateTaskConfiguration(data);
        await ValidateQuestionnairesExistAsync(data.QuestionnaireIds);

        var now = Utilities.GetCurrentTimeStampIso();

        await _dynamoClient.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue($"{DynamoTableKeys.TaskPkPrefix}{taskKey}"),
                ["SK"] = new AttributeValue(DynamoTableKeys.MetadataSk)
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
                ["PK"] = new AttributeValue($"{DynamoTableKeys.TaskPkPrefix}{taskKey}"),
                ["SK"] = new AttributeValue(DynamoTableKeys.MetadataSk)
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

        // Canonicalize common variants 
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

    private static void ValidateTaskConfiguration(TaskData task)
    {
        var type = task.Type;
        var questionnaireIds = task.QuestionnaireIds ?? new List<string>();
        var configuration = task.Configuration ?? new Dictionary<string, object>();

        switch (type)
        {
            case "Training":
            case "NeuroGame":
                if (questionnaireIds.Any())
                    throw new ArgumentException($"{type} tasks must not define QuestionnaireIds.");

                var sceneName = GetRequiredConfigurationString(configuration, "sceneName");
                if (string.IsNullOrWhiteSpace(sceneName))
                    throw new ArgumentException($"{type} tasks must define Configuration.sceneName.");

                if (configuration.TryGetValue("hasFeedback", out var hasFeedbackValue) &&
                    !TryParseBoolean(hasFeedbackValue, out _))
                {
                    throw new ArgumentException($"{type} tasks Configuration.hasFeedback must be a boolean when provided.");
                }
                break;

            case "Questionnaire":
                if (questionnaireIds.Count != 1)
                    throw new ArgumentException("Questionnaire tasks must define exactly one QuestionnaireId in QuestionnaireIds.");

                if (configuration.ContainsKey("sceneName"))
                    throw new ArgumentException("Questionnaire tasks must not define Configuration.sceneName.");
                break;

            case "QuestionnaireSet":
                if (!questionnaireIds.Any())
                    throw new ArgumentException("QuestionnaireSet tasks must define at least one QuestionnaireId in QuestionnaireIds.");

                if (configuration.ContainsKey("sceneName"))
                    throw new ArgumentException("QuestionnaireSet tasks must not define Configuration.sceneName.");
                break;

            default:
                throw new ArgumentException($"Unsupported task Type '{type}'. Supported: Training, NeuroGame, Questionnaire, QuestionnaireSet.");
        }
    }
    private static string GetRequiredConfigurationString(Dictionary<string, object> configuration, string key)
    {
        if (!configuration.TryGetValue(key, out var value) || value == null)
            return string.Empty;

        return value.ToString()?.Trim() ?? string.Empty;
    }

    private static bool TryParseBoolean(object? value, out bool result)
    {
        if (value is bool b)
        {
            result = b;
            return true;
        }

        return bool.TryParse(value?.ToString(), out result);
    }
    private async Task ValidateQuestionnairesExistAsync(IEnumerable<string>? questionnaireIds)
    {
        if (questionnaireIds == null)
            return;

        var ids = questionnaireIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ids.Count == 0)
            return;

        const int batchSize = 100;
        var foundIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < ids.Count; i += batchSize)
        {
            var chunk = ids.Skip(i).Take(batchSize).ToList();

            var requestItems = new Dictionary<string, KeysAndAttributes>
            {
                [_questionnairesTable] = new KeysAndAttributes
                {
                    Keys = chunk.Select(id => new Dictionary<string, AttributeValue>
                    {
                        ["PK"] = new AttributeValue { S = $"{DynamoTableKeys.QuestionnairePkPrefix}{id}" },
                        ["SK"] = new AttributeValue { S = DynamoTableKeys.ConfigSk }
                    }).ToList()
                }
            };

            const int maxRetries = 5;
            var attempt = 0;

            while (requestItems.Count > 0)
            {
                BatchGetItemResponse response;

                try
                {
                    response = await _dynamoClient.BatchGetItemAsync(new BatchGetItemRequest
                    {
                        RequestItems = requestItems
                    });
                }
                catch (ResourceNotFoundException ex)
                {
                    throw new InvalidOperationException(
                        $"Questionnaire table '{_questionnairesTable}' was not found while validating questionnaires.",
                        ex);
                }
                catch (AmazonDynamoDBException ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to validate questionnaires in table '{_questionnairesTable}'. DynamoDB error: {ex.Message}",
                        ex);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        "Unexpected error while validating questionnaire references.",
                        ex);
                }

                if (response.Responses.TryGetValue(_questionnairesTable, out var items) && items != null)
                {
                    foreach (var item in items)
                    {
                        var isDeleted = item.GetValueOrDefault("syncMetadata")?.M?
                            .GetValueOrDefault("isDeleted")?.BOOL ?? false;

                        if (isDeleted)
                            continue;

                        var pk = item.GetValueOrDefault("PK")?.S ?? string.Empty;
                        if (pk.StartsWith(DynamoTableKeys.QuestionnairePkPrefix, StringComparison.OrdinalIgnoreCase))
                        {
                            foundIds.Add(pk.Substring(DynamoTableKeys.QuestionnairePkPrefix.Length));
                        }
                    }
                }

                requestItems = response.UnprocessedKeys ?? new Dictionary<string, KeysAndAttributes>();

                if (requestItems.Count > 0)
                {
                    attempt++;
                    if (attempt > maxRetries)
                        throw new TimeoutException("BatchGetItem exceeded retry limit while validating questionnaire references.");

                    var delayMs = (int)(50 * Math.Pow(2, attempt)) + Random.Shared.Next(0, 75);
                    await Task.Delay(delayMs);
                }
            }
        }

        var missing = ids
            .Where(id => !foundIds.Contains(id))
            .ToList();

        if (missing.Count > 0)
            throw new ArgumentException($"Missing questionnaires: {string.Join(", ", missing)}");
    }
}
