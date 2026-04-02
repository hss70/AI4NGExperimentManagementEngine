using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGExperimentsLambda.Interfaces.Participant;
using AI4NGExperimentsLambda.Models;
using AI4NGExperimentsLambda.Models.Dtos;
using AI4NGExperimentsLambda.Mappers;
using AI4NGExperimentsLambda.Models.Dtos.Responses;
using AI4NGExperimentsLambda.Models.Requests.Participant;
using AI4NGExperimentsLambda.Models.Constants;
using System.Globalization;

namespace AI4NGExperimentsLambda.Services.Participant;

public sealed class ParticipantSessionOccurrencesService : IParticipantSessionOccurrencesService
{
    private readonly IAmazonDynamoDB _dynamo;
    private readonly string _experimentsTable;
    private readonly string _responsesTable;

    public ParticipantSessionOccurrencesService(IAmazonDynamoDB dynamo)
    {
        _dynamo = dynamo ?? throw new ArgumentNullException(nameof(dynamo));

        _experimentsTable = Environment.GetEnvironmentVariable("EXPERIMENTS_TABLE") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_experimentsTable))
            throw new InvalidOperationException("EXPERIMENTS_TABLE environment variable is not set");

        _responsesTable = Environment.GetEnvironmentVariable("RESPONSES_TABLE") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_responsesTable))
            throw new InvalidOperationException("RESPONSES_TABLE environment variable is not set");
    }

    public async Task<IReadOnlyList<OccurrenceDto>> ListOccurrencesAsync(
        string experimentId,
        string participantId,
        string? from = null,
        string? to = null,
        CancellationToken ct = default)
    {
        experimentId = Guard.RequireExperimentId(experimentId);
        participantId = Guard.RequireParticipantId(participantId);

        await EnsureParticipantMembershipAsync(experimentId, participantId, ct);

        var resp = await _dynamo.QueryAsync(new QueryRequest
        {
            TableName = _experimentsTable,
            KeyConditionExpression = "PK = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new AttributeValue
                {
                    S = OccurrenceItemMapper.BuildPk(experimentId, participantId)
                }
            },
            ScanIndexForward = true
        }, ct);

        var list = resp.Items
            .Select(OccurrenceItemMapper.MapOccurrenceFromItem)
            .Select(OccurrenceItemMapper.MapToDto)
            .ToList();

        // Optional date filtering for now in-memory
        if (!string.IsNullOrWhiteSpace(from))
            list = list.Where(x => string.CompareOrdinal(x.ScheduledAt, from) >= 0).ToList();

        if (!string.IsNullOrWhiteSpace(to))
            list = list.Where(x => string.CompareOrdinal(x.ScheduledAt, to) <= 0).ToList();

        return list;
    }

    public async Task<OccurrenceDto?> GetOccurrenceAsync(
        string experimentId,
        string participantId,
        string occurrenceKey,
        CancellationToken ct = default)
    {
        experimentId = Guard.RequireExperimentId(experimentId);
        participantId = Guard.RequireParticipantId(participantId);
        occurrenceKey = Guard.RequireOccurrenceKey(occurrenceKey);

        await EnsureParticipantMembershipAsync(experimentId, participantId, ct);

        var resp = await _dynamo.GetItemAsync(new GetItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = OccurrenceItemMapper.BuildPk(experimentId, participantId) },
                ["SK"] = new AttributeValue { S = occurrenceKey }
            },
            ConsistentRead = true
        }, ct);

        if (!resp.IsItemSet)
            return null;

        var occurrence = OccurrenceItemMapper.MapOccurrenceFromItem(resp.Item);
        return OccurrenceItemMapper.MapToDto(occurrence);
    }

    public async Task<OccurrenceDto> CreateOccurrenceAsync(
        string experimentId,
        string participantId,
        CreateOccurrenceRequest request,
        CancellationToken ct = default)
    {
        experimentId = Guard.RequireExperimentId(experimentId);
        participantId = Guard.RequireParticipantId(participantId);

        if (request == null)
            throw new ArgumentException("Create occurrence request is required");

        await EnsureParticipantMembershipAsync(experimentId, participantId, ct);

        var nowIso = DateTime.UtcNow.ToString("O");
        var occurrenceKey = BuildOccurrenceKey(request);

        var taskOrder = await ResolveTaskOrderAsync(experimentId, request.SessionTypeKey, ct);

        var occurrence = new ParticipantSessionOccurrence
        {
            ExperimentId = experimentId,
            ParticipantId = participantId,
            OccurrenceKey = occurrenceKey,
            ProtocolSessionKey = NormalizeOptional(request.ProtocolSessionKey),
            SessionTypeKey = Guard.RequireSessionTypeKey(request.SessionTypeKey),
            OccurrenceType = string.IsNullOrWhiteSpace(request.OccurrenceType)
                ? OccurrenceTypes.Optional
                : request.OccurrenceType.Trim(),
            IsRequired = request.IsRequired,
            Status = OccurrenceStatuses.Scheduled,
            ScheduledAt = NormalizeOptional(request.ScheduledAt) ?? nowIso,
            Timezone = NormalizeOptional(request.Timezone),
            Source = NormalizeOptional(request.Source) ?? OccurrenceSources.ParticipantInitiated,
            TaskOrder = taskOrder,
            TotalTaskCount = taskOrder.Count,
            CompletedTaskCount = 0,
            CurrentTaskIndex = 0,
            TaskState = taskOrder
                .Select((taskKey, index) => new OccurrenceTaskState
                {
                    Order = index + 1,
                    TaskKey = taskKey,
                    Status = OccurrenceTaskStatuses.Pending,
                    IsRequired = true
                })
                .ToList(),
            CreatedAt = nowIso,
            UpdatedAt = nowIso
        };

        var item = OccurrenceItemMapper.MapToItem(occurrence, participantId, participantId);

        await _dynamo.PutItemAsync(new PutItemRequest
        {
            TableName = _experimentsTable,
            Item = item,
            ConditionExpression = "attribute_not_exists(PK) AND attribute_not_exists(SK)"
        }, ct);

        return OccurrenceItemMapper.MapToDto(occurrence);
    }

    public async Task<OccurrenceDto> StartOccurrenceAsync(
        string experimentId,
        string participantId,
        string occurrenceKey,
        StartOccurrenceRequest? request = null,
        CancellationToken ct = default)
    {
        var occurrence = await LoadOccurrenceOrThrowAsync(experimentId, participantId, occurrenceKey, ct);

        if (string.Equals(occurrence.Status, OccurrenceStatuses.Completed, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Completed occurrence cannot be restarted");

        var nowIso = DateTime.UtcNow.ToString("O");

        occurrence.Status = OccurrenceStatuses.InProgress;
        occurrence.StartedAt ??= nowIso;
        occurrence.LastResumedAt = nowIso;
        occurrence.UpdatedAt = nowIso;
        occurrence.UpdatedBy = participantId;

        await SaveOccurrenceAsync(occurrence, participantId, ct);
        return OccurrenceItemMapper.MapToDto(occurrence);
    }

    public async Task<OccurrenceDto> CompleteOccurrenceAsync(
        string experimentId,
        string participantId,
        string occurrenceKey,
        CompleteOccurrenceRequest? request = null,
        CancellationToken ct = default)
    {
        var occurrence = await LoadOccurrenceOrThrowAsync(experimentId, participantId, occurrenceKey, ct);

        var nowIso = DateTime.UtcNow.ToString("O");

        if (request?.MarkIncompleteTasksAsSkipped == true)
        {
            foreach (var task in occurrence.TaskState.Where(x =>
                         !string.Equals(x.Status, OccurrenceTaskStatuses.Completed, StringComparison.OrdinalIgnoreCase) &&
                         !string.Equals(x.Status, OccurrenceTaskStatuses.Skipped, StringComparison.OrdinalIgnoreCase)))
            {
                task.Status = OccurrenceTaskStatuses.Skipped;
                task.EndedAt ??= nowIso;
            }
        }

        occurrence.CompletedTaskCount = occurrence.TaskState.Count(x =>
            string.Equals(x.Status, OccurrenceTaskStatuses.Completed, StringComparison.OrdinalIgnoreCase));

        occurrence.Status = OccurrenceStatuses.Completed;
        occurrence.EndedAt = nowIso;
        occurrence.UpdatedAt = nowIso;
        occurrence.UpdatedBy = participantId;

        if (!string.IsNullOrWhiteSpace(request?.Notes))
            occurrence.Notes = request.Notes.Trim();

        await SaveOccurrenceAsync(occurrence, participantId, ct);
        return OccurrenceItemMapper.MapToDto(occurrence);
    }

    public async Task<OccurrenceDto> SubmitTaskResponseAsync(
        string experimentId,
        string participantId,
        string occurrenceKey,
        string taskKey,
        SubmitTaskResponseRequest request,
        CancellationToken ct = default)
    {
        experimentId = Guard.RequireExperimentId(experimentId);
        participantId = Guard.RequireParticipantId(participantId);
        occurrenceKey = Guard.RequireOccurrenceKey(occurrenceKey);
        taskKey = RequireTaskKey(taskKey);
        request = request ?? throw new ArgumentException("Submit task response request is required");

        var clientSubmissionId = RequireClientSubmissionId(request.ClientSubmissionId);
        if (request.Payload == null)
            throw new ArgumentException("Payload is required");

        var occurrence = await LoadOccurrenceOrThrowAsync(experimentId, participantId, occurrenceKey, ct);

        if (!string.Equals(occurrence.Status, OccurrenceStatuses.InProgress, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Occurrence status must be '{OccurrenceStatuses.InProgress}' to submit a task response");

        var expectedTaskKey = occurrence.CurrentTaskIndex >= 0 &&
                              occurrence.CurrentTaskIndex < occurrence.TaskOrder.Count
            ? occurrence.TaskOrder[occurrence.CurrentTaskIndex]
            : null;

        if (!string.Equals(expectedTaskKey, taskKey, StringComparison.OrdinalIgnoreCase))
        {
            var completedTask = occurrence.TaskState.FirstOrDefault(x =>
                string.Equals(x.TaskKey, taskKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Status, OccurrenceTaskStatuses.Completed, StringComparison.OrdinalIgnoreCase));

            if (completedTask != null)
            {
                var existing = await LoadTaskResponseAsync(experimentId, participantId, occurrenceKey, taskKey, ct);

                if (existing != null &&
                    string.Equals(existing.ClientSubmissionId, clientSubmissionId, StringComparison.Ordinal))
                {
                    return OccurrenceItemMapper.MapToDto(occurrence);
                }

                throw new InvalidOperationException("Task has already been completed with a different submission");
            }

            throw new ArgumentException($"Task '{taskKey}' is not the current task");
        }

        if (occurrence.CurrentTaskIndex < 0 || occurrence.CurrentTaskIndex >= occurrence.TaskOrder.Count)
            throw new InvalidOperationException("Occurrence has no remaining current task");

        var nowIso = DateTime.UtcNow.ToString("O");
        var responseId = Guid.NewGuid().ToString();

        var response = new TaskResponse
        {
            ResponseId = responseId,
            ExperimentId = experimentId,
            ParticipantId = participantId,
            OccurrenceKey = occurrenceKey,
            TaskKey = taskKey,
            QuestionnaireId = NormalizeOptional(request.QuestionnaireId),
            Payload = request.Payload,
            ClientSubmissionId = clientSubmissionId,
            ClientSubmittedAt = NormalizeOptional(request.ClientSubmittedAt),
            SubmittedAt = nowIso,
            CreatedAt = nowIso
        };

        var taskState = occurrence.TaskState.FirstOrDefault(x =>
            string.Equals(x.TaskKey, taskKey, StringComparison.OrdinalIgnoreCase));

        if (taskState == null)
            throw new KeyNotFoundException($"Task state for '{taskKey}' was not found");

        taskState.Status = OccurrenceTaskStatuses.Completed;
        taskState.StartedAt ??= nowIso;
        taskState.EndedAt = nowIso;

        occurrence.CompletedTaskCount = occurrence.TaskState.Count(x =>
            string.Equals(x.Status, OccurrenceTaskStatuses.Completed, StringComparison.OrdinalIgnoreCase));

        occurrence.CurrentTaskIndex += 1;
        occurrence.UpdatedAt = nowIso;
        occurrence.UpdatedBy = participantId;

        if (occurrence.CurrentTaskIndex >= occurrence.TaskOrder.Count)
        {
            occurrence.Status = OccurrenceStatuses.Completed;
            occurrence.EndedAt = nowIso;
        }

        var occurrenceItem = OccurrenceItemMapper.MapToItem(
            occurrence,
            string.IsNullOrWhiteSpace(occurrence.CreatedBy) ? participantId : occurrence.CreatedBy,
            participantId);

        var responseItem = TaskResponseItemMapper.MapToItem(response);
        await _dynamo.TransactWriteItemsAsync(new TransactWriteItemsRequest
        {
            TransactItems = new List<TransactWriteItem>
            {
                new()
                {
                    Put = new Put
                    {
                        TableName = _responsesTable,
                        Item = responseItem,
                        ConditionExpression = "attribute_not_exists(PK) AND attribute_not_exists(SK)"
                    }
                },
                new()
                {
                    Put = new Put
                    {
                        TableName = _experimentsTable,
                        Item = occurrenceItem,
                        ConditionExpression = "attribute_exists(PK) AND attribute_exists(SK) AND #data.#status = :inProgress AND #data.#currentTaskIndex = :expectedIndex",
                        ExpressionAttributeNames = new Dictionary<string, string>
                        {
                            ["#data"] = "data",
                            ["#status"] = "Status",
                            ["#currentTaskIndex"] = "CurrentTaskIndex"
                        },
                        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                        {
                            [":inProgress"] = new AttributeValue { S = OccurrenceStatuses.InProgress },
                            [":expectedIndex"] = new AttributeValue { N = (occurrence.CurrentTaskIndex - 1).ToString() }
                        }
                    }
                }
            }
        }, ct);

        return OccurrenceItemMapper.MapToDto(occurrence);
    }

    public async Task<ResolveOccurrenceDto> ResolveCurrentOccurrenceAsync(
        string experimentId,
        string participantId,
        CancellationToken ct = default)
    {
        experimentId = Guard.RequireExperimentId(experimentId);
        participantId = Guard.RequireParticipantId(participantId);

        //await EnsureParticipantMembershipAsync(experimentId, participantId, ct);
        var membership = await LoadParticipantMembershipAsync(experimentId, participantId, ct);

        if (!string.Equals(membership.Status, "active", StringComparison.OrdinalIgnoreCase))
        {
            return new ResolveOccurrenceDto
            {
                ResolutionType = "participant_inactive"
            };
        }

        // 1. Resume any in-progress occurrence first
        var existingDtos = await ListOccurrencesAsync(experimentId, participantId, ct: ct);

        var inProgress = existingDtos
            .Where(x => string.Equals(x.Status, OccurrenceStatuses.InProgress, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefault();

        if (inProgress != null)
        {
            return new ResolveOccurrenceDto
            {
                ResolutionType = "resume",
                Occurrence = inProgress
            };
        }

        // 2. Load experiment + protocol sessions
        var experiment = await LoadExperimentOrThrowAsync(experimentId, ct);
        var protocolSessions = await LoadProtocolSessionsAsync(experimentId, ct);

        if (protocolSessions.Count == 0)
        {
            return new ResolveOccurrenceDto
            {
                ResolutionType = "none_available",
                AvailableActions = BuildOptionalActions(experiment)
            };
        }

        var nowUtc = DateTime.UtcNow;
        var scheduleContext = BuildParticipantScheduleContext(membership, experiment, nowUtc);

        if (!scheduleContext.HasStarted)
        {
            return new ResolveOccurrenceDto
            {
                ResolutionType = "not_started_yet"
            };
        }

        if (!scheduleContext.IsWithinWindow)
        {
            return new ResolveOccurrenceDto
            {
                ResolutionType = "outside_schedule_window"
            };
        }

        var todayLocal = scheduleContext.LocalToday;
        var isoYear = ISOWeek.GetYear(todayLocal.ToDateTime(TimeOnly.MinValue));
        var isoWeek = ISOWeek.GetWeekOfYear(todayLocal.ToDateTime(TimeOnly.MinValue));

        // 3. Work through required protocol sessions in order
        foreach (var protocol in protocolSessions.OrderBy(x => x.Order))
        {
            var baseKey = GetRequiredOccurrenceBaseKey(protocol, todayLocal, isoYear, isoWeek);
            if (baseKey == null)
                continue;

            var matchingOccurrences = existingDtos
                .Where(x =>
                    string.Equals(x.OccurrenceKey, baseKey, StringComparison.OrdinalIgnoreCase) ||
                    x.OccurrenceKey.StartsWith(baseKey + "#", StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => GetOccurrenceSlotNumber(x.OccurrenceKey, baseKey))
                .ToList();

            var scheduled = matchingOccurrences
                .FirstOrDefault(x => string.Equals(x.Status, OccurrenceStatuses.Scheduled, StringComparison.OrdinalIgnoreCase));

            if (scheduled != null)
            {
                return new ResolveOccurrenceDto
                {
                    ResolutionType = "start_required",
                    Occurrence = scheduled
                };
            }

            var maxRepeats = protocol.MaxPerDay ?? 1;
            if (matchingOccurrences.Count >= maxRepeats)
                continue;

            var nextSlot = matchingOccurrences.Count + 1;
            var occurrenceKey = BuildRequiredOccurrenceSlotKey(baseKey, nextSlot);

            var created = await CreateRequiredOccurrenceAsync(
                experiment,
                participantId,
                protocol,
                occurrenceKey,
                scheduleContext,
                ct);

            return new ResolveOccurrenceDto
            {
                ResolutionType = "start_required",
                Occurrence = created
            };
        }

        // 4. No required work due right now -> offer optional actions
        return new ResolveOccurrenceDto
        {
            ResolutionType = "none_available",
            AvailableActions = BuildOptionalActions(experiment)
        };
    }

    private async Task<ParticipantSessionOccurrence> LoadOccurrenceOrThrowAsync(
        string experimentId,
        string participantId,
        string occurrenceKey,
        CancellationToken ct)
    {
        experimentId = Guard.RequireExperimentId(experimentId);
        participantId = Guard.RequireParticipantId(participantId);
        occurrenceKey = Guard.RequireOccurrenceKey(occurrenceKey);

        await EnsureParticipantMembershipAsync(experimentId, participantId, ct);

        var resp = await _dynamo.GetItemAsync(new GetItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = OccurrenceItemMapper.BuildPk(experimentId, participantId) },
                ["SK"] = new AttributeValue { S = occurrenceKey }
            },
            ConsistentRead = true
        }, ct);

        if (!resp.IsItemSet)
            throw new KeyNotFoundException($"Occurrence '{occurrenceKey}' was not found");

        return OccurrenceItemMapper.MapOccurrenceFromItem(resp.Item);
    }

    private async Task SaveOccurrenceAsync(
        ParticipantSessionOccurrence occurrence,
        string updatedBy,
        CancellationToken ct)
    {
        occurrence.UpdatedAt = DateTime.UtcNow.ToString("O");
        occurrence.UpdatedBy = updatedBy;

        var item = OccurrenceItemMapper.MapToItem(
            occurrence,
            string.IsNullOrWhiteSpace(occurrence.CreatedBy) ? updatedBy : occurrence.CreatedBy,
            updatedBy);

        await _dynamo.PutItemAsync(new PutItemRequest
        {
            TableName = _experimentsTable,
            Item = item
        }, ct);
    }

    private async Task<TaskResponse?> LoadTaskResponseAsync(
        string experimentId,
        string participantId,
        string occurrenceKey,
        string taskKey,
        CancellationToken ct)
    {
        var responsePk = TaskResponseItemMapper.BuildPk(experimentId, participantId, occurrenceKey, taskKey);

        var resp = await _dynamo.GetItemAsync(new GetItemRequest
        {
            TableName = _responsesTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = responsePk },
                ["SK"] = new AttributeValue { S = TaskResponseItemMapper.BuildSk() }
            },
            ConsistentRead = true
        }, ct);

        if (!resp.IsItemSet)
            return null;

        return TaskResponseItemMapper.MapFromItem(resp.Item);
    }

    private async Task EnsureParticipantMembershipAsync(
        string experimentId,
        string participantId,
        CancellationToken ct)
    {
        var resp = await _dynamo.GetItemAsync(new GetItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = $"{DynamoTableKeys.ExperimentPkPrefix}{experimentId}" },
                ["SK"] = new AttributeValue { S = $"{DynamoTableKeys.MemberSkPrefix}{participantId}" }
            },
            ConsistentRead = true
        }, ct);

        if (!resp.IsItemSet)
            throw new UnauthorizedAccessException("Participant is not enrolled in this experiment");
    }

    private async Task<List<string>> ResolveTaskOrderAsync(
        string experimentId,
        string sessionTypeKey,
        CancellationToken ct)
    {
        var resp = await _dynamo.GetItemAsync(new GetItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = $"{DynamoTableKeys.ExperimentPkPrefix}{experimentId}" },
                ["SK"] = new AttributeValue { S = DynamoTableKeys.MetadataSk }
            },
            ConsistentRead = true
        }, ct);

        if (!resp.IsItemSet)
            throw new KeyNotFoundException($"Experiment '{experimentId}' was not found");

        var experiment = ExperimentItemMapper.MapExperimentDataFromItem(resp.Item);

        if (experiment.SessionTypes == null ||
            !experiment.SessionTypes.TryGetValue(sessionTypeKey, out var sessionType) ||
            sessionType == null)
        {
            throw new ArgumentException($"SessionTypeKey '{sessionTypeKey}' does not exist");
        }

        return (sessionType.Tasks ?? new List<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToList();
    }

    private static string BuildOccurrenceKey(CreateOccurrenceRequest request)
    {
        var occurrenceType = (request.OccurrenceType ?? string.Empty).Trim();

        return occurrenceType switch
        {
            var x when x.Equals(OccurrenceTypes.FreePlay, StringComparison.OrdinalIgnoreCase)
                => OccurrenceKeyHelper.BuildOptionalKey("FREEPLAY"),

            var x when x.Equals(OccurrenceTypes.QuestionBank, StringComparison.OrdinalIgnoreCase)
                => OccurrenceKeyHelper.BuildOptionalKey("QUESTIONBANK"),

            _ => OccurrenceKeyHelper.BuildOptionalKey("OPTIONAL")
        };
    }

    private static string? GetRequiredOccurrenceKey(
    ProtocolSessionDto protocol,
    DateOnly todayLocal,
    int isoYear,
    int isoWeek)
    {
        var cadence = (protocol.CadenceType ?? string.Empty).Trim().ToUpperInvariant();

        return cadence switch
        {
            "ONCE" => string.IsNullOrWhiteSpace(protocol.ProtocolKey)
                ? "FIRST"
                : protocol.ProtocolKey,

            "DAILY" => OccurrenceKeyHelper.BuildDailyKey(todayLocal),

            "WEEKLY" => OccurrenceKeyHelper.BuildWeeklyKey(isoYear, isoWeek),

            _ => null
        };
    }
    private static string? GetRequiredOccurrenceBaseKey(
    ProtocolSessionDto protocol,
    DateOnly todayLocal,
    int isoYear,
    int isoWeek)
    {
        var cadence = (protocol.CadenceType ?? string.Empty).Trim().ToUpperInvariant();

        return cadence switch
        {
            "ONCE" => string.IsNullOrWhiteSpace(protocol.ProtocolKey)
                ? "FIRST"
                : protocol.ProtocolKey,

            "DAILY" => OccurrenceKeyHelper.BuildDailyKey(todayLocal),

            "WEEKLY" => OccurrenceKeyHelper.BuildWeeklyKey(isoYear, isoWeek),

            _ => null
        };
    }

    private static string BuildRequiredOccurrenceSlotKey(string baseKey, int slotNumber)
    {
        if (slotNumber <= 1)
            return baseKey;

        return $"{baseKey}#{slotNumber}";
    }

    private static int GetOccurrenceSlotNumber(string occurrenceKey, string baseKey)
    {
        if (string.Equals(occurrenceKey, baseKey, StringComparison.OrdinalIgnoreCase))
            return 1;

        var suffix = occurrenceKey.Substring(baseKey.Length);
        if (!suffix.StartsWith("#", StringComparison.Ordinal))
            return int.MaxValue;

        return int.TryParse(suffix[1..], out var slotNumber) && slotNumber > 0
            ? slotNumber
            : int.MaxValue;
    }
    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string RequireTaskKey(string value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException("Task key is required");

        return trimmed;
    }

    private static string RequireClientSubmissionId(string value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException("ClientSubmissionId is required");

        return trimmed;
    }

    private async Task<ExperimentDto> LoadExperimentOrThrowAsync(
string experimentId,
CancellationToken ct)
    {
        var resp = await _dynamo.GetItemAsync(new GetItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = $"{DynamoTableKeys.ExperimentPkPrefix}{experimentId}" },
                ["SK"] = new AttributeValue { S = DynamoTableKeys.MetadataSk }
            },
            ConsistentRead = true
        }, ct);

        if (!resp.IsItemSet)
            throw new KeyNotFoundException($"Experiment '{experimentId}' was not found");

        var item = resp.Item;

        return new ExperimentDto
        {
            Id = experimentId,
            Status = item.GetValueOrDefault("status")?.S ?? string.Empty,
            Data = ExperimentItemMapper.MapExperimentDataFromItem(item),
            UpdatedAt = item.GetValueOrDefault("updatedAt")?.S,
            UpdatedBy = item.GetValueOrDefault("updatedBy")?.S,
            CreatedAt = item.GetValueOrDefault("createdAt")?.S,
            CreatedBy = item.GetValueOrDefault("createdBy")?.S ?? string.Empty
        };
    }

    private async Task<List<ProtocolSessionDto>> LoadProtocolSessionsAsync(
    string experimentId,
    CancellationToken ct)
    {
        var resp = await _dynamo.QueryAsync(new QueryRequest
        {
            TableName = _experimentsTable,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :skprefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new AttributeValue { S = $"{DynamoTableKeys.ExperimentPkPrefix}{experimentId}" },
                [":skprefix"] = new AttributeValue { S = DynamoTableKeys.ProtocolSessionSkPrefix }
            }
        }, ct);

        var list = new List<ProtocolSessionDto>();

        foreach (var item in resp.Items)
        {
            var sk = item.GetValueOrDefault("SK")?.S ?? string.Empty;
            var protocolKey = sk.StartsWith(DynamoTableKeys.ProtocolSessionSkPrefix, StringComparison.OrdinalIgnoreCase)
                ? sk.Substring(DynamoTableKeys.ProtocolSessionSkPrefix.Length)
                : sk;

            list.Add(ProtocolSessionItemMapper.MapProtocolSessionDto(experimentId, protocolKey, item));
        }

        return list
            .OrderBy(x => x.Order)
            .ToList();
    }

    private async Task<OccurrenceDto> CreateRequiredOccurrenceAsync(
        ExperimentDto experiment,
        string participantId,
        ProtocolSessionDto protocol,
        string occurrenceKey,
        ParticipantScheduleContext scheduleContext,
        CancellationToken ct)
    {
        var taskOrder = await ResolveTaskOrderAsync(
            experiment.Id,
            protocol.SessionTypeKey,
            ct);

        var nowIso = scheduleContext.UtcNow.ToString("O");

        var occurrence = new ParticipantSessionOccurrence
        {
            ExperimentId = experiment.Id,
            ParticipantId = participantId,
            OccurrenceKey = occurrenceKey,
            ProtocolSessionKey = protocol.ProtocolKey,
            SessionTypeKey = protocol.SessionTypeKey,
            OccurrenceType = OccurrenceTypes.Protocol,
            IsRequired = true,
            Status = OccurrenceStatuses.Scheduled,
            ScheduledAt = nowIso,
            DateLocal = scheduleContext.LocalToday.ToString("yyyy-MM-dd"),
            StudyDay = scheduleContext.StudyDay,
            StudyWeek = scheduleContext.StudyWeek,
            Timezone = scheduleContext.Timezone,
            Source = OccurrenceSources.Scheduled,
            TaskOrder = taskOrder,
            TotalTaskCount = taskOrder.Count,
            CompletedTaskCount = 0,
            CurrentTaskIndex = 0,
            TaskState = taskOrder
                .Select((taskKey, index) => new OccurrenceTaskState
                {
                    Order = index + 1,
                    TaskKey = taskKey,
                    Status = OccurrenceTaskStatuses.Pending,
                    IsRequired = true
                })
                .ToList(),
            CreatedAt = nowIso,
            CreatedBy = participantId,
            UpdatedAt = nowIso,
            UpdatedBy = participantId
        };

        var item = OccurrenceItemMapper.MapToItem(occurrence, participantId, participantId);

        await _dynamo.PutItemAsync(new PutItemRequest
        {
            TableName = _experimentsTable,
            Item = item,
            ConditionExpression = "attribute_not_exists(PK) AND attribute_not_exists(SK)"
        }, ct);

        return OccurrenceItemMapper.MapToDto(occurrence);
    }

    private static List<OccurrenceActionDto> BuildOptionalActions(ExperimentDto experiment)
    {
        var actions = new List<OccurrenceActionDto>();

        foreach (var kvp in experiment.Data?.SessionTypes ?? new Dictionary<string, SessionType>())
        {
            var sessionTypeKey = (kvp.Key ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(sessionTypeKey))
                continue;

            var sessionType = kvp.Value ?? new SessionType();

            actions.Add(new OccurrenceActionDto
            {
                ActionKey = $"start-optional-{sessionTypeKey.ToLowerInvariant()}",
                Label = string.IsNullOrWhiteSpace(sessionType.Name)
                    ? $"Start optional {sessionTypeKey}"
                    : $"Start optional {sessionType.Name}",
                OccurrenceType = OccurrenceTypes.Optional,
                SessionTypeKey = sessionTypeKey,
                IsRequired = false
            });
        }

        return actions;
    }

    private async Task<ExperimentMemberDto> LoadParticipantMembershipAsync(
    string experimentId,
    string participantId,
    CancellationToken ct)
    {
        var resp = await _dynamo.GetItemAsync(new GetItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = $"{DynamoTableKeys.ExperimentPkPrefix}{experimentId}" },
                ["SK"] = new AttributeValue { S = $"{DynamoTableKeys.MemberSkPrefix}{participantId}" }
            },
            ConsistentRead = true
        }, ct);

        if (!resp.IsItemSet || resp.Item == null || resp.Item.Count == 0)
            throw new UnauthorizedAccessException("Participant is not enrolled in this experiment");

        return ExperimentMemberItemMapper.MapMemberDto(resp.Item);
    }


    private sealed class ParticipantScheduleContext
    {
        public string Timezone { get; init; } = "UTC";
        public DateTime UtcNow { get; init; }
        public DateTime LocalNow { get; init; }
        public DateOnly LocalToday { get; init; }

        public DateOnly? ParticipantStartDate { get; init; }
        public DateOnly? ParticipantEndDate { get; init; }
        public int? ParticipantDurationDaysOverride { get; init; }

        public int? StudyDay { get; init; }
        public int? StudyWeek { get; init; }

        public bool HasStarted { get; init; }
        public bool IsWithinWindow { get; init; }
    }

    private static ParticipantScheduleContext BuildParticipantScheduleContext(
    ExperimentMemberDto membership,
    ExperimentDto experiment,
    DateTime utcNow)
    {
        var timezone = string.IsNullOrWhiteSpace(membership.Timezone)
            ? "UTC"
            : membership.Timezone.Trim();

        TimeZoneInfo tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
        }
        catch
        {
            tz = TimeZoneInfo.Utc;
            timezone = "UTC";
        }

        var localNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, tz);
        var localToday = DateOnly.FromDateTime(localNow);

        var participantStartDate = TryParseDateOnly(membership.ParticipantStartDate)
            ?? TryParseDateOnly(experiment.Data?.StudyStartDate);

        var participantEndDate = TryParseDateOnly(membership.ParticipantEndDate);

        var durationDays = membership.ParticipantDurationDaysOverride
            ?? experiment.Data?.ParticipantDurationDays;

        if (!participantEndDate.HasValue &&
            participantStartDate.HasValue &&
            durationDays.HasValue &&
            durationDays.Value > 0)
        {
            participantEndDate = participantStartDate.Value.AddDays(durationDays.Value - 1);
        }

        var hasStarted = !participantStartDate.HasValue || localToday >= participantStartDate.Value;
        var isWithinWindow =
            hasStarted &&
            (!participantEndDate.HasValue || localToday <= participantEndDate.Value);

        int? studyDay = null;
        int? studyWeek = null;

        if (participantStartDate.HasValue && localToday >= participantStartDate.Value)
        {
            studyDay = localToday.DayNumber - participantStartDate.Value.DayNumber + 1;
            studyWeek = ((studyDay.Value - 1) / 7) + 1;
        }

        return new ParticipantScheduleContext
        {
            Timezone = timezone,
            UtcNow = utcNow,
            LocalNow = localNow,
            LocalToday = localToday,
            ParticipantStartDate = participantStartDate,
            ParticipantEndDate = participantEndDate,
            ParticipantDurationDaysOverride = membership.ParticipantDurationDaysOverride,
            StudyDay = studyDay,
            StudyWeek = studyWeek,
            HasStarted = hasStarted,
            IsWithinWindow = isWithinWindow
        };
    }

    private static DateOnly? TryParseDateOnly(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return DateOnly.TryParse(value, out var parsed)
            ? parsed
            : null;
    }
}
