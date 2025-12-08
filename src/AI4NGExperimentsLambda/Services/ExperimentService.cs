using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGExperimentsLambda.Interfaces;
using AI4NGExperimentsLambda.Models;
using AI4NGExperimentsLambda.Models.Dtos;
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

    public async Task<IEnumerable<ExperimentListDto>> GetExperimentsAsync()
    {
        var response = await _dynamoClient.ScanAsync(new ScanRequest
        {
            TableName = _experimentsTable,
            FilterExpression = "#type = :type",
            ExpressionAttributeNames = new Dictionary<string, string> { ["#type"] = "type" },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":type"] = new("Experiment") }
        });

        return response.Items.Select(item => new ExperimentListDto
        {
            Id = item.ContainsKey("PK") ? item["PK"].S.Replace("EXPERIMENT#", "") : string.Empty,
            Name = item.ContainsKey("data") && item["data"].M.ContainsKey("Name") ? item["data"].M["Name"].S : string.Empty,
            Description = item.ContainsKey("data") && item["data"].M.ContainsKey("Description") ? item["data"].M["Description"].S : string.Empty
        });
    }

    public async Task<ExperimentDto?> GetExperimentAsync(string? experimentId)
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

        return new ExperimentDto
        {
            Id = experimentId!,
            Data = (DynamoDBHelper.ConvertAttributeValueToObject(experiment["data"]) as ExperimentData) ?? new ExperimentData(),
            QuestionnaireConfig = (DynamoDBHelper.ConvertAttributeValueToObject(experiment["questionnaireConfig"]) as QuestionnaireConfig) ?? new QuestionnaireConfig(),
            UpdatedAt = experiment.GetValueOrDefault("updatedAt")?.S
        };
    }

    public async Task<IEnumerable<ExperimentListDto>> GetMyExperimentsAsync(string username)
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

        var results = new List<ExperimentListDto>();
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

            results.Add(new ExperimentListDto { Id = expId, Name = name, Description = description, Role = role });
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

        // Normalize experiment.data to include questionnaireIds consistently
        var dataMap = DynamoDBHelper.JsonToAttributeValue(JsonSerializer.SerializeToElement(experiment.Data));
        if (!dataMap.ContainsKey("questionnaireIds"))
        {
            dataMap["questionnaireIds"] = new AttributeValue
            {
                L = questionnaireIds.Select(q => new AttributeValue(q)).ToList()
            };
        }

        await _dynamoClient.PutItemAsync(new PutItemRequest
        {
            TableName = _experimentsTable,
            Item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"EXPERIMENT#{experimentId}"),
                ["SK"] = new("METADATA"),
                ["type"] = new("Experiment"),
                ["data"] = new AttributeValue { M = dataMap },
                ["questionnaireConfig"] = new AttributeValue { M = DynamoDBHelper.JsonToAttributeValue(JsonSerializer.SerializeToElement(experiment.QuestionnaireConfig)) },
                ["createdBy"] = new(username),
                ["createdAt"] = new(DateTime.UtcNow.ToString("O")),
                ["updatedAt"] = new(DateTime.UtcNow.ToString("O"))
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
        // Normalize incoming data to include questionnaireIds derived from session types and questionnaireConfig
        var normalizedDataMap = DynamoDBHelper.JsonToAttributeValue(JsonSerializer.SerializeToElement(data));
        var derivedQuestionnaireIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (data.SessionTypes != null)
        {
            foreach (var st in data.SessionTypes.Values)
            {
                if (st?.Questionnaires != null)
                {
                    foreach (var q in st.Questionnaires)
                    {
                        if (!string.IsNullOrWhiteSpace(q)) derivedQuestionnaireIds.Add(q);
                    }
                }
            }
        }
        // QuestionnaireConfig is serialized separately; here we only ensure data.questionnaireIds exists
        normalizedDataMap["questionnaireIds"] = new AttributeValue
        {
            L = derivedQuestionnaireIds.Select(q => new AttributeValue(q)).ToList()
        };

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
                [":data"] = new AttributeValue { M = normalizedDataMap },
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

    public async Task<ExperimentSyncDto> SyncExperimentAsync(string? experimentId, DateTime? lastSyncTime, string username)
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

        // Build session DTOs and collect referenced task IDs
        var sessionDtos = new List<SessionDto>();
        var referencedTaskIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in sessions)
        {
            var sid = s.GetValueOrDefault("GSI1SK")?.S?.Split('#').Last();
            var sdataObj = DynamoDBHelper.ConvertAttributeValueToObject(s.GetValueOrDefault("data")) as Dictionary<string, object> ?? new Dictionary<string, object>();
            var taskOrderAttr = s.GetValueOrDefault("taskOrder");
            var taskOrder = DynamoDBHelper.ConvertAttributeValueToObject(taskOrderAttr) as List<string> ?? new List<string>();
            // Enrich common fields from data with sensible fallbacks
            var sessionType = sdataObj.TryGetValue("SessionType", out var stObj) && stObj is string st ? st : (s.GetValueOrDefault("sessionType")?.S ?? string.Empty);
            var description = sdataObj.TryGetValue("Description", out var descObj) && descObj is string desc ? desc : (s.GetValueOrDefault("description")?.S ?? string.Empty);
            var date = sdataObj.TryGetValue("Date", out var dateObj) && dateObj is string dt ? dt : sid ?? string.Empty;
            var sessionName = sdataObj.TryGetValue("SessionName", out var snObj) && snObj is string sn ? sn : (s.GetValueOrDefault("sessionName")?.S ?? string.Empty);
            // Update sdataObj with enriched values so clients see populated fields
            sdataObj["SessionType"] = sessionType;
            sdataObj["Description"] = description;
            sdataObj["Date"] = date;
            sdataObj["SessionName"] = sessionName;
            foreach (var t in taskOrder)
            {
                if (t.StartsWith("TASK#")) referencedTaskIds.Add(t.Substring(5));
            }
            sessionDtos.Add(new SessionDto
            {
                SessionId = sid,
                Data = JsonSerializer.Deserialize<SessionData>(JsonSerializer.Serialize(sdataObj)) ?? new SessionData(),
                TaskOrder = taskOrder,
                CreatedAt = s.GetValueOrDefault("createdAt")?.S,
                UpdatedAt = s.GetValueOrDefault("updatedAt")?.S
            });
        }

        // Fetch tasks referenced by sessions in a BatchGet
        var tasks = new List<TaskDto>();
        if (referencedTaskIds.Count > 0)
        {
            var keys = referencedTaskIds.Select(id => new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"TASK#{id}"),
                ["SK"] = new("METADATA")
            }).ToList();

            var batchReq = new BatchGetItemRequest
            {
                RequestItems = new Dictionary<string, KeysAndAttributes>
                {
                    [_experimentsTable] = new KeysAndAttributes
                    {
                        Keys = keys,
                        ProjectionExpression = "PK, data, createdAt, updatedAt"
                    }
                }
            };

            var batchResp = await _dynamoClient.BatchGetItemAsync(batchReq);
            if (batchResp.Responses.TryGetValue(_experimentsTable, out var taskItems))
            {
                foreach (var item in taskItems)
                {
                    var id = item["PK"].S.Replace("TASK#", "");
                    var tdataObj = DynamoDBHelper.ConvertAttributeValueToObject(item.GetValueOrDefault("data"));
                    tasks.Add(new TaskDto
                    {
                        Id = id,
                        Data = JsonSerializer.Deserialize<TaskData>(JsonSerializer.Serialize(tdataObj)) ?? new TaskData(),
                        CreatedAt = item.GetValueOrDefault("createdAt")?.S,
                        UpdatedAt = item.GetValueOrDefault("updatedAt")?.S
                    });
                }
            }
        }

        // Aggregate questionnaires needed: from experiment data and task configurations
        var questionnaireIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var expDataElem = experiment.GetValueOrDefault("data");
        var expDataObj = DynamoDBHelper.ConvertAttributeValueToObject(expDataElem) as Dictionary<string, object> ?? new Dictionary<string, object>();
        if (expDataObj.TryGetValue("questionnaireIds", out var qidsObj) && qidsObj is IEnumerable<object> qarr)
        {
            foreach (var q in qarr)
                if (q is string qs && !string.IsNullOrWhiteSpace(qs)) questionnaireIds.Add(qs);
        }
        foreach (var t in tasks)
        {
            var tdict = t as IDictionary<string, object>;
            if (tdict != null && tdict.TryGetValue("data", out var tdataObj) && tdataObj is IDictionary<string, object> tdata)
            {
                if (tdata.TryGetValue("Configuration", out var cfgObj) && cfgObj is IDictionary<string, object> cfg)
                {
                    if (cfg.TryGetValue("questionnaireId", out var qidObj) && qidObj is string qid && !string.IsNullOrWhiteSpace(qid))
                        questionnaireIds.Add(qid);
                }
                // Also support flat questionnaireId
                if (tdata.TryGetValue("questionnaireId", out var flatQidObj) && flatQidObj is string fqid && !string.IsNullOrWhiteSpace(fqid))
                    questionnaireIds.Add(fqid);
            }
        }

        // Aggregate session names and types for quick glance
        var sessionNames = sessionDtos
            .Select(d => d.Data?.SessionName)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!)
            .Distinct()
            .ToArray();
        var sessionTypes = sessionDtos
            .Select(d => d.Data?.SessionType)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!)
            .Distinct()
            .ToArray();

        // Ensure experiment.data includes up-to-date questionnaireIds prior to returning
        expDataObj["questionnaireIds"] = questionnaireIds.ToArray();

        return new ExperimentSyncDto
        {
            Experiment = experiment != null ? new ExperimentDto
            {
                Id = experimentId!,
                Data = JsonSerializer.Deserialize<ExperimentData>(JsonSerializer.Serialize(expDataObj)) ?? new ExperimentData(),
                QuestionnaireConfig = (DynamoDBHelper.ConvertAttributeValueToObject(experiment.GetValueOrDefault("questionnaireConfig")) as QuestionnaireConfig) ?? new QuestionnaireConfig(),
                UpdatedAt = experiment.GetValueOrDefault("updatedAt")?.S
            } : null,
            Sessions = sessionDtos,
            Tasks = tasks,
            Questionnaires = questionnaireIds.ToList(),
            SessionNames = sessionNames.ToList(),
            SessionTypes = sessionTypes.ToList(),
            SyncTimestamp = DateTime.UtcNow.ToString("O")
        };
    }

    public async Task<IEnumerable<MemberDto>> GetExperimentMembersAsync(string experimentId, string? cohort = null, string? status = null, string? role = null)
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

        var list = response.Items.Select(item => new MemberDto
        {
            Username = item["SK"].S.Replace("MEMBER#", ""),
            Role = item.ContainsKey("role") ? item["role"].S : "participant",
            Status = item.ContainsKey("status") ? item["status"].S : "active",
            Cohort = item.ContainsKey("cohort") ? item["cohort"].S : string.Empty,
            JoinedAt = item.ContainsKey("addedAt") ? item["addedAt"].S : null
        });

        if (!string.IsNullOrWhiteSpace(cohort))
            list = list.Where(m => string.Equals(m.Cohort, cohort, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(status))
            list = list.Where(m => string.Equals(m.Status, status, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(role))
            list = list.Where(m => string.Equals(m.Role, role, StringComparison.OrdinalIgnoreCase));
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
    public async Task<IEnumerable<SessionDto>> GetExperimentSessionsAsync(string experimentId)
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

        return response.Items.Where(i => i["type"].S == "Session").Select(item =>
        {
            var sdataObj = DynamoDBHelper.ConvertAttributeValueToObject(item["data"]);
            return new SessionDto
            {
                SessionId = item["PK"].S,
                Data = JsonSerializer.Deserialize<SessionData>(JsonSerializer.Serialize(sdataObj)) ?? new SessionData(),
                TaskOrder = item.ContainsKey("taskOrder") ? (DynamoDBHelper.ConvertAttributeValueToObject(item["taskOrder"]) as List<string> ?? new List<string>()) : new List<string>(),
                CreatedAt = item["createdAt"]?.S,
                UpdatedAt = item["updatedAt"]?.S
            };
        });
    }

    public async Task<SessionDto?> GetSessionAsync(string experimentId, string sessionId)
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

        var sdataObj = DynamoDBHelper.ConvertAttributeValueToObject(response.Item["data"]);
        return new SessionDto
        {
            SessionId = sessionId,
            ExperimentId = experimentId,
            Data = JsonSerializer.Deserialize<SessionData>(JsonSerializer.Serialize(sdataObj)) ?? new SessionData(),
            TaskOrder = response.Item.ContainsKey("taskOrder") ? (DynamoDBHelper.ConvertAttributeValueToObject(response.Item["taskOrder"]) as List<string> ?? new List<string>()) : new List<string>(),
            CreatedAt = response.Item["createdAt"]?.S,
            UpdatedAt = response.Item["updatedAt"]?.S
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

        // Persist session name into experiment data for quick glance
        if (!string.IsNullOrWhiteSpace(request.SessionName))
        {
            await UpsertExperimentSessionNameAsync(experimentId, request.SessionName);
        }

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

        // Touch experiment updatedAt to reflect session change
        await _dynamoClient.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"EXPERIMENT#{experimentId}"),
                ["SK"] = new("METADATA")
            },
            UpdateExpression = "SET updatedAt = :timestamp",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":timestamp"] = new(DateTime.UtcNow.ToString("O"))
            }
        });

        // Persist session name updates into experiment data
        if (!string.IsNullOrWhiteSpace(data.SessionName))
        {
            await UpsertExperimentSessionNameAsync(experimentId, data.SessionName);
        }
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

        // Optional: could remove session name from experiment data if we also fetch the session's name.
        // Skipping removal to avoid losing historical visibility; clients can rely on sync for current sessions.
    }

    private async Task UpsertExperimentSessionNameAsync(string experimentId, string sessionName)
    {
        var expResp = await _dynamoClient.GetItemAsync(new GetItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"EXPERIMENT#{experimentId}"),
                ["SK"] = new("METADATA")
            }
        });

        if (!expResp.IsItemSet || !expResp.Item.TryGetValue("data", out var dataAttr)) return;

        var expDataObj = DynamoDBHelper.ConvertAttributeValueToObject(dataAttr) as Dictionary<string, object> ?? new Dictionary<string, object>();
        List<string> names;
        if (expDataObj.TryGetValue("SessionNames", out var existing) && existing is IEnumerable<object> arr)
        {
            names = arr
                .Select(x => x?.ToString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        else
        {
            names = new List<string>();
        }
        if (!names.Contains(sessionName, StringComparer.OrdinalIgnoreCase))
        {
            names.Add(sessionName);
        }
        expDataObj["SessionNames"] = names;

        await _dynamoClient.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"EXPERIMENT#{experimentId}"),
                ["SK"] = new("METADATA")
            },
            UpdateExpression = "SET #data = :data, updatedAt = :timestamp",
            ExpressionAttributeNames = new Dictionary<string, string> { ["#data"] = "data" },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":data"] = new AttributeValue { M = DynamoDBHelper.JsonToAttributeValue(JsonSerializer.SerializeToElement(expDataObj)) },
                [":timestamp"] = new(DateTime.UtcNow.ToString("O"))
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