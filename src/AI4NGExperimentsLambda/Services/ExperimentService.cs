using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGExperimentsLambda.Interfaces;
using AI4NGExperimentsLambda.Models;
using AI4NGExperimentManagement.Shared;
using System.Text.Json;

namespace AI4NGExperimentsLambda.Services;

public class ExperimentService : IExperimentService
{
    private readonly IAmazonDynamoDB _dynamoClient;
    private readonly string _experimentsTable;
    private readonly string _responsesTable;

    public ExperimentService(IAmazonDynamoDB dynamoClient)
    {
        _dynamoClient = dynamoClient;
        _experimentsTable = Environment.GetEnvironmentVariable("EXPERIMENTS_TABLE") ?? string.Empty;
        _responsesTable = Environment.GetEnvironmentVariable("RESPONSES_TABLE") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(_experimentsTable))
            throw new InvalidOperationException("EXPERIMENTS_TABLE environment variable is not set");
    }

    public async Task<IEnumerable<object>> GetExperimentsAsync()
    {
        var response = await _dynamoClient.ScanAsync(new ScanRequest
        {
            TableName = _experimentsTable,
            FilterExpression = "#type = :type",
            ExpressionAttributeNames = new Dictionary<string, string> { ["#type"] = "type" },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":type"] = new("Experiment") }
        });

        return response.Items.Select(item => new
        {
            id = item.ContainsKey("PK") ? item["PK"].S.Replace("EXPERIMENT#", "") : string.Empty,
            name = item.ContainsKey("data") && item["data"].M.ContainsKey("Name") ? item["data"].M["Name"].S : string.Empty,
            description = item.ContainsKey("data") && item["data"].M.ContainsKey("Description") ? item["data"].M["Description"].S : string.Empty
        });
    }

    public async Task<object?> GetExperimentAsync(string? experimentId)
    {
        if (string.IsNullOrWhiteSpace(experimentId))
            return null;

        var response = await _dynamoClient.QueryAsync(new QueryRequest
        {
            TableName = _experimentsTable,
            KeyConditionExpression = "PK = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"EXPERIMENT#{experimentId}")
            }
        });

        if (!response.Items.Any())
            return null;

        var experiment = response.Items.First(i => i["SK"].S == "METADATA");
        var sessions = response.Items.Where(i => i["SK"].S.StartsWith("SESSION#")).ToList();

        return new
        {
            id = experimentId,
            data = DynamoDBHelper.ConvertAttributeValueToObject(experiment["data"]),
            questionnaireConfig = DynamoDBHelper.ConvertAttributeValueToObject(experiment["questionnaireConfig"]),
            sessions = sessions.Select(s => DynamoDBHelper.ConvertAttributeValueToObject(s["data"]))
        };
    }

    public async Task<IEnumerable<object>> GetMyExperimentsAsync(string username)
    {
        var response = await _dynamoClient.QueryAsync(new QueryRequest
        {
            TableName = _experimentsTable,
            IndexName = "GSI1",
            KeyConditionExpression = "GSI1PK = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"USER#{username}")
            }
        });

        return response.Items.Select(item => new
        {
            id = item["PK"].S.Replace("EXPERIMENT#", ""),
            name = item["data"].M["Name"].S,
            description = item["data"].M.GetValueOrDefault("Description")?.S ?? "",
            role = item["role"]?.S ?? "participant"
        });
    }

    public async Task<object> CreateExperimentAsync(Experiment experiment, string username)
    {
        // Collect all questionnaire IDs from sessionTypes and questionnaireConfig
        var questionnaireIds = new HashSet<string>();

        // From sessionTypes
        if (experiment.Data.SessionTypes != null)
        {
            foreach (var sessionType in experiment.Data.SessionTypes.Values)
            {
                if (sessionType.Questionnaires != null)
                {
                    foreach (var qId in sessionType.Questionnaires)
                    {
                        questionnaireIds.Add(qId);
                    }
                }
            }
        }

        // From questionnaireConfig.schedule
        if (experiment.QuestionnaireConfig?.Schedule != null)
        {
            foreach (var qId in experiment.QuestionnaireConfig.Schedule.Keys)
            {
                questionnaireIds.Add(qId);
            }
        }

        // Validate questionnaire existence
        foreach (var questionnaireId in questionnaireIds)
        {
            var questionnaireExists = await _dynamoClient.GetItemAsync(new GetItemRequest
            {
                TableName = Environment.GetEnvironmentVariable("QUESTIONNAIRES_TABLE") ?? "AI4NGQuestionnaires-dev",
                Key = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new($"QUESTIONNAIRE#{questionnaireId}"),
                    ["SK"] = new("CONFIG")
                }
            });

            if (!questionnaireExists.IsItemSet)
                throw new InvalidOperationException($"Questionnaire '{questionnaireId}' not found");
        }

        var experimentId = experiment.Id ?? Guid.NewGuid().ToString();

        await _dynamoClient.PutItemAsync(new PutItemRequest
        {
            TableName = _experimentsTable,
            Item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"EXPERIMENT#{experimentId}"),
                ["SK"] = new("METADATA"),
                ["type"] = new("Experiment"),
                ["data"] = new AttributeValue { M = DynamoDBHelper.JsonToAttributeValue(JsonSerializer.SerializeToElement(experiment.Data)) },
                ["questionnaireConfig"] = new AttributeValue { M = DynamoDBHelper.JsonToAttributeValue(JsonSerializer.SerializeToElement(experiment.QuestionnaireConfig)) },
                ["createdBy"] = new(username),
                ["createdAt"] = new(DateTime.UtcNow.ToString("O"))
            }
        });

        return new { id = experimentId };
    }

    public async Task UpdateExperimentAsync(string experimentId, ExperimentData data, string username)
    {
        await _dynamoClient.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"EXPERIMENT#{experimentId}"),
                ["SK"] = new("METADATA")
            },
            UpdateExpression = "SET #data = :data, updatedBy = :user, updatedAt = :timestamp",
            ExpressionAttributeNames = new Dictionary<string, string> { ["#data"] = "data" },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":data"] = new AttributeValue { M = DynamoDBHelper.JsonToAttributeValue(JsonSerializer.SerializeToElement(data)) },
                [":user"] = new(username),
                [":timestamp"] = new(DateTime.UtcNow.ToString("O"))
            }
        });
    }

    public async Task DeleteExperimentAsync(string experimentId, string username)
    {
        await _dynamoClient.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"EXPERIMENT#{experimentId}"),
                ["SK"] = new("METADATA")
            }
        });
    }

    public async Task<object> SyncExperimentAsync(string? experimentId, DateTime? lastSyncTime, string username)
    {
        if (string.IsNullOrWhiteSpace(experimentId))
            throw new ArgumentException("Experiment ID is required");

        // Validate experiment exists
        var experimentExists = await _dynamoClient.GetItemAsync(new GetItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"EXPERIMENT#{experimentId}"),
                ["SK"] = new("METADATA")
            }
        });

        if (!experimentExists.IsItemSet)
            throw new InvalidOperationException($"Experiment '{experimentId}' not found");

        var filterExpression = "PK = :pk";
        var expressionValues = new Dictionary<string, AttributeValue>
        {
            [":pk"] = new($"EXPERIMENT#{experimentId}")
        };

        // Add timestamp filter if provided
        if (lastSyncTime.HasValue)
        {
            filterExpression += " AND updatedAt > :lastSync";
            expressionValues[":lastSync"] = new(lastSyncTime.Value.ToString("O"));
        }

        var response = await _dynamoClient.QueryAsync(new QueryRequest
        {
            TableName = _experimentsTable,
            KeyConditionExpression = filterExpression,
            ExpressionAttributeValues = expressionValues
        });

        var experiment = response.Items?.FirstOrDefault(i => i["SK"].S == "METADATA");
        var sessions = response.Items?.Where(i => i["SK"].S.StartsWith("SESSION#")).ToList() ?? new List<Dictionary<string, AttributeValue>>();

        return new
        {
            experiment = experiment != null ? new
            {
                id = experimentId,
                data = DynamoDBHelper.ConvertAttributeValueToObject(experiment["data"]),
                questionnaireConfig = DynamoDBHelper.ConvertAttributeValueToObject(experiment.GetValueOrDefault("questionnaireConfig")),
                updatedAt = experiment.GetValueOrDefault("updatedAt")?.S
            } : null,
            sessions = sessions.Select(s => new
            {
                data = DynamoDBHelper.ConvertAttributeValueToObject(s["data"]),
                updatedAt = s.GetValueOrDefault("updatedAt")?.S
            }),
            syncTimestamp = DateTime.UtcNow.ToString("O")
        };
    }

    public async Task<IEnumerable<object>> GetExperimentMembersAsync(string experimentId)
    {
        var response = await _dynamoClient.QueryAsync(new QueryRequest
        {
            TableName = _experimentsTable,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :sk)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"EXPERIMENT#{experimentId}"),
                [":sk"] = new("MEMBER#")
            }
        });

        return response.Items.Select(item => new
        {
            userSub = item["SK"].S.Replace("MEMBER#", ""),
            role = item["role"]?.S ?? "participant",
            addedAt = item["addedAt"]?.S
        });
    }

    public async Task AddMemberAsync(string experimentId, string userSub, MemberRequest memberData, string username)
    {
        await _dynamoClient.PutItemAsync(new PutItemRequest
        {
            TableName = _experimentsTable,
            Item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"EXPERIMENT#{experimentId}"),
                ["SK"] = new($"MEMBER#{userSub}"),
                ["type"] = new("Member"),
                ["role"] = new(memberData.Role),
                ["addedBy"] = new(username),
                ["addedAt"] = new(DateTime.UtcNow.ToString("O"))
            }
        });
    }

    public async Task RemoveMemberAsync(string experimentId, string userSub, string username)
    {
        await _dynamoClient.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"EXPERIMENT#{experimentId}"),
                ["SK"] = new($"MEMBER#{userSub}")
            }
        });
    }

    // Session management methods
    public async Task<IEnumerable<object>> GetExperimentSessionsAsync(string experimentId)
    {
        var response = await _dynamoClient.QueryAsync(new QueryRequest
        {
            TableName = _experimentsTable,
            IndexName = "GSI1",
            KeyConditionExpression = "GSI1PK = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"EXPERIMENT#{experimentId}")
            }
        });

        return response.Items.Where(i => i["type"].S == "Session").Select(item => new
        {
            sessionId = item["PK"].S,
            data = DynamoDBHelper.ConvertAttributeValueToObject(item["data"]),
            taskOrder = item.ContainsKey("taskOrder") ? DynamoDBHelper.ConvertAttributeValueToObject(item["taskOrder"]) : new List<string>(),
            createdAt = item["createdAt"]?.S,
            updatedAt = item["updatedAt"]?.S
        });
    }

    public async Task<object?> GetSessionAsync(string experimentId, string sessionId)
    {
        var response = await _dynamoClient.GetItemAsync(new GetItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"SESSION#{experimentId}#{sessionId}"),
                ["SK"] = new("METADATA")
            }
        });

        if (!response.IsItemSet)
            return null;

        return new
        {
            sessionId = sessionId,
            experimentId = experimentId,
            data = DynamoDBHelper.ConvertAttributeValueToObject(response.Item["data"]),
            taskOrder = response.Item.ContainsKey("taskOrder") ? DynamoDBHelper.ConvertAttributeValueToObject(response.Item["taskOrder"]) : new List<string>(),
            createdAt = response.Item["createdAt"]?.S,
            updatedAt = response.Item["updatedAt"]?.S
        };
    }

    public async Task<object> CreateSessionAsync(string experimentId, CreateSessionRequest request, string username)
    {
        var sessionId = $"{request.Date}";
        var sessionPK = $"SESSION#{experimentId}#{sessionId}";

        // Get experiment to determine session type configuration
        var experiment = await GetExperimentAsync(experimentId);
        if (experiment == null)
            throw new InvalidOperationException($"Experiment '{experimentId}' not found");

        var sessionData = new SessionData
        {
            Date = request.Date,
            SessionType = request.SessionType,
            Status = "scheduled",
            UserId = username
        };

        await _dynamoClient.PutItemAsync(new PutItemRequest
        {
            TableName = _experimentsTable,
            Item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new(sessionPK),
                ["SK"] = new("METADATA"),
                ["GSI1PK"] = new($"EXPERIMENT#{experimentId}"),
                ["GSI1SK"] = new(sessionPK),
                ["type"] = new("Session"),
                ["data"] = new AttributeValue { M = DynamoDBHelper.JsonToAttributeValue(JsonSerializer.SerializeToElement(sessionData)) },
                ["taskOrder"] = new AttributeValue { L = new List<AttributeValue>() },
                ["createdAt"] = new(DateTime.UtcNow.ToString("O")),
                ["updatedAt"] = new(DateTime.UtcNow.ToString("O"))
            }
        });

        return new { sessionId = sessionId, experimentId = experimentId };
    }

    public async Task UpdateSessionAsync(string experimentId, string sessionId, SessionData data, string username)
    {
        await _dynamoClient.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"SESSION#{experimentId}#{sessionId}"),
                ["SK"] = new("METADATA")
            },
            UpdateExpression = "SET #data = :data, updatedAt = :timestamp",
            ExpressionAttributeNames = new Dictionary<string, string> { ["#data"] = "data" },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":data"] = new AttributeValue { M = DynamoDBHelper.JsonToAttributeValue(JsonSerializer.SerializeToElement(data)) },
                [":timestamp"] = new(DateTime.UtcNow.ToString("O"))
            }
        });
    }

    public async Task DeleteSessionAsync(string experimentId, string sessionId, string username)
    {
        await _dynamoClient.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"SESSION#{experimentId}#{sessionId}"),
                ["SK"] = new("METADATA")
            }
        });
    }


}