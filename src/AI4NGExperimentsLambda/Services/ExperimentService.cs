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
        var members = response.Items.Where(i => i["SK"].S.StartsWith("MEMBER#")).Select(m => new
        {
            username = m["SK"].S.Replace("MEMBER#", ""),
            role = m.ContainsKey("role") ? m["role"].S : "participant",
            status = m.ContainsKey("status") ? m["status"].S : "active",
            cohort = m.ContainsKey("cohort") ? m["cohort"].S : string.Empty,
            addedAt = m.ContainsKey("addedAt") ? m["addedAt"].S : null
        });

        return new
        {
            id = experimentId,
            data = DynamoDBHelper.ConvertAttributeValueToObject(experiment["data"]),
            questionnaireConfig = DynamoDBHelper.ConvertAttributeValueToObject(experiment["questionnaireConfig"]),
            sessions = sessions.Select(s => DynamoDBHelper.ConvertAttributeValueToObject(s["data"])),
            users = members
        };
    }

    public async Task<IEnumerable<object>> GetMyExperimentsAsync(string username)
    {
        // Query membership items via GSI1 (USER#username)
        var membershipQuery = await _dynamoClient.QueryAsync(new QueryRequest
        {
            TableName = _experimentsTable,
            IndexName = "GSI1",
            KeyConditionExpression = "GSI1PK = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"USER#{username}")
            }
        });

        var results = new List<object>();
        foreach (var item in membershipQuery.Items)
        {
            var expId = item["PK"].S.Replace("EXPERIMENT#", "");
            var role = item.ContainsKey("role") ? item["role"].S : "participant";

            // Fetch experiment metadata to get name/description
            var meta = await _dynamoClient.GetItemAsync(new GetItemRequest
            {
                TableName = _experimentsTable,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new($"EXPERIMENT#{expId}"),
                    ["SK"] = new("METADATA")
                }
            });

            string name = string.Empty;
            string description = string.Empty;
            if (meta.IsItemSet && meta.Item.ContainsKey("data"))
            {
                var dataMap = meta.Item["data"].M;
                if (dataMap.ContainsKey("Name")) name = dataMap["Name"].S;
                if (dataMap.ContainsKey("Description")) description = dataMap["Description"].S;
            }

            results.Add(new { id = expId, name, description, role });
        }

        return results;
    }

    public async Task<object> ValidateExperimentAsync(Experiment experiment)
    {
        var questionnaireIds = CollectQuestionnaireIds(experiment);
        var missingQuestionnaires = await ValidateQuestionnaires(questionnaireIds);

        return new
        {
            valid = !missingQuestionnaires.Any(),
            referencedQuestionnaires = questionnaireIds.ToList(),
            missingQuestionnaires = missingQuestionnaires,
            message = missingQuestionnaires.Any()
                ? $"Missing questionnaires: {string.Join(", ", missingQuestionnaires)}"
                : "All dependencies are valid"
        };
    }

    public async Task<object> CreateExperimentAsync(Experiment experiment, string username)
    {
        var questionnaireIds = CollectQuestionnaireIds(experiment);
        var missingQuestionnaires = await ValidateQuestionnaires(questionnaireIds);

        if (missingQuestionnaires.Any())
        {
            var errorMessage = $"Missing questionnaires: {string.Join(", ", missingQuestionnaires)}";
            Console.Error.WriteLine($"Experiment creation failed: {errorMessage}");
            throw new ArgumentException(errorMessage);
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

        // Optionally create initial sessions
        if (experiment.InitialSessions != null && experiment.InitialSessions.Any())
        {
            foreach (var seed in experiment.InitialSessions)
            {
                var sessionId = !string.IsNullOrWhiteSpace(seed.SessionId)
                    ? seed.SessionId!
                    : (!string.IsNullOrWhiteSpace(seed.Date) ? seed.Date : DateTime.UtcNow.ToString("yyyy-MM-dd"));

                // Derive taskOrder from session type if not provided
                List<string> taskOrder = seed.TaskOrder ?? new List<string>();
                if (taskOrder.Count == 0 && experiment.Data.SessionTypes != null && experiment.Data.SessionTypes.TryGetValue(seed.SessionType, out var st))
                {
                    taskOrder = st.Tasks.Select(t => t.StartsWith("TASK#") ? t : $"TASK#{t}").ToList();
                }

                var sessionData = new SessionData
                {
                    Date = string.IsNullOrWhiteSpace(seed.Date) ? sessionId : seed.Date,
                    SessionType = seed.SessionType,
                    SessionName = $"{seed.SessionType} Session",
                    Description = "Seeded at experiment creation",
                    Status = "scheduled",
                    UserId = username
                };

                await _dynamoClient.PutItemAsync(new PutItemRequest
                {
                    TableName = _experimentsTable,
                    Item = new Dictionary<string, AttributeValue>
                    {
                        ["PK"] = new($"SESSION#{experimentId}#{sessionId}"),
                        ["SK"] = new("METADATA"),
                        ["GSI1PK"] = new($"EXPERIMENT#{experimentId}"),
                        ["GSI1SK"] = new($"SESSION#{experimentId}#{sessionId}"),
                        ["type"] = new("Session"),
                        ["data"] = new AttributeValue { M = DynamoDBHelper.JsonToAttributeValue(JsonSerializer.SerializeToElement(sessionData)) },
                        ["taskOrder"] = new AttributeValue { L = taskOrder.Select(x => new AttributeValue(x)).ToList() },
                        ["createdAt"] = new(DateTime.UtcNow.ToString("O")),
                        ["updatedAt"] = new(DateTime.UtcNow.ToString("O"))
                    }
                });
            }
        }

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

        // Fetch experiment metadata directly
        var experiment = experimentExists.Item;

        // Fetch sessions via GSI1 to align with access pattern
        var sessionsQuery = await _dynamoClient.QueryAsync(new QueryRequest
        {
            TableName = _experimentsTable,
            IndexName = "GSI1",
            KeyConditionExpression = "GSI1PK = :pk AND begins_with(GSI1SK, :skprefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"EXPERIMENT#{experimentId}"),
                [":skprefix"] = new($"SESSION#{experimentId}#")
            }
        });

        var sessions = sessionsQuery.Items ?? new List<Dictionary<string, AttributeValue>>();

        return new
        {
            experiment = experiment != null ? new
            {
                id = experimentId,
                data = DynamoDBHelper.ConvertAttributeValueToObject(experiment.GetValueOrDefault("data")),
                questionnaireConfig = DynamoDBHelper.ConvertAttributeValueToObject(experiment.GetValueOrDefault("questionnaireConfig")),
                updatedAt = experiment.GetValueOrDefault("updatedAt")?.S
            } : null,
            sessions = sessions.Select(s => new
            {
                sessionId = s.GetValueOrDefault("GSI1SK")?.S?.Split('#').Last(),
                data = DynamoDBHelper.ConvertAttributeValueToObject(s.GetValueOrDefault("data")),
                taskOrder = DynamoDBHelper.ConvertAttributeValueToObject(s.GetValueOrDefault("taskOrder")) ?? new List<string>(),
                createdAt = s.GetValueOrDefault("createdAt")?.S,
                updatedAt = s.GetValueOrDefault("updatedAt")?.S
            }),
            syncTimestamp = DateTime.UtcNow.ToString("O")
        };
    }

    public async Task<IEnumerable<object>> GetExperimentMembersAsync(string experimentId, string? cohort = null, string? status = null, string? role = null)
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

        var list = response.Items.Select(item => new
        {
            participantUsername = item["SK"].S.Replace("MEMBER#", ""),
            role = item.ContainsKey("role") ? item["role"].S : "participant",
            status = item.ContainsKey("status") ? item["status"].S : "active",
            cohort = item.ContainsKey("cohort") ? item["cohort"].S : string.Empty,
            addedAt = item.ContainsKey("addedAt") ? item["addedAt"].S : null
        });

        if (!string.IsNullOrWhiteSpace(cohort))
            list = list.Where(m => string.Equals(m.cohort, cohort, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(status))
            list = list.Where(m => string.Equals(m.status, status, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(role))
            list = list.Where(m => string.Equals(m.role, role, StringComparison.OrdinalIgnoreCase));

        return list;
    }

    public async Task AddMemberAsync(string experimentId, string participantUsername, MemberRequest memberData, string username)
    {
        await _dynamoClient.PutItemAsync(new PutItemRequest
        {
            TableName = _experimentsTable,
            Item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"EXPERIMENT#{experimentId}"),
                ["SK"] = new($"MEMBER#{participantUsername}"),
                ["type"] = new("Member"),
                ["role"] = new(memberData.Role),
                ["status"] = new(memberData.Status ?? "active"),
                ["cohort"] = new(string.IsNullOrWhiteSpace(memberData.Cohort) ? "" : memberData.Cohort),
                ["addedBy"] = new(username),
                ["addedAt"] = new(DateTime.UtcNow.ToString("O")),
                // Add GSI entries to support listing by user
                ["GSI1PK"] = new($"USER#{participantUsername}"),
                ["GSI1SK"] = new($"EXPERIMENT#{experimentId}")
            }
        });
    }

    public async Task AddMembersAsync(string experimentId, IEnumerable<MemberBatchItem> members, string username)
    {
        // Simple sequential writes; switch to BatchWriteItem for large batches if needed
        foreach (var m in members)
        {
            var req = new MemberRequest { Role = m.Role, Status = m.Status, Cohort = m.Cohort };
            await AddMemberAsync(experimentId, m.Username, req, username);
        }
    }

    public async Task RemoveMemberAsync(string experimentId, string participantUsername, string username)
    {
        await _dynamoClient.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"EXPERIMENT#{experimentId}"),
                ["SK"] = new($"MEMBER#{participantUsername}")
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
            SessionName = request.SessionName,
            Description = request.Description,
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

    public async Task AddSessionsAsync(string experimentId, IEnumerable<CreateSessionRequest> sessions, string username)
    {
        if (sessions == null) return;
        foreach (var s in sessions)
        {
            // Ensure experimentId consistency
            s.ExperimentId = experimentId;
            // Create is idempotent on the PK; will overwrite metadata if same sessionId
            await CreateSessionAsync(experimentId, s, username);
        }
    }

    public async Task UpdateSessionAsync(string experimentId, string sessionId, SessionData data, string username)
    {
        var updateExpr = "SET #data = :data, updatedAt = :timestamp";
        var exprAttrNames = new Dictionary<string, string> { ["#data"] = "data" };
        var exprAttrValues = new Dictionary<string, AttributeValue>
        {
            [":data"] = new AttributeValue { M = DynamoDBHelper.JsonToAttributeValue(JsonSerializer.SerializeToElement(data)) },
            [":timestamp"] = new(DateTime.UtcNow.ToString("O"))
        };

        // If task order provided, include it in update
        if (data.TaskOrder != null)
        {
            updateExpr += ", taskOrder = :taskOrder";
            exprAttrValues[":taskOrder"] = new AttributeValue { L = data.TaskOrder.Select(t => new AttributeValue(t)).ToList() };
        }

        await _dynamoClient.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"SESSION#{experimentId}#{sessionId}"),
                ["SK"] = new("METADATA")
            },
            UpdateExpression = updateExpr,
            ExpressionAttributeNames = exprAttrNames,
            ExpressionAttributeValues = exprAttrValues
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

    private HashSet<string> CollectQuestionnaireIds(Experiment experiment)
    {
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

        return questionnaireIds;
    }

    private async Task<List<string>> ValidateQuestionnaires(HashSet<string> questionnaireIds)
    {
        var missingQuestionnaires = new List<string>();

        foreach (var questionnaireId in questionnaireIds)
        {
            try
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
                {
                    missingQuestionnaires.Add(questionnaireId);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error checking questionnaire '{questionnaireId}': {ex.Message}");
                missingQuestionnaires.Add(questionnaireId);
            }
        }

        return missingQuestionnaires;
    }
}