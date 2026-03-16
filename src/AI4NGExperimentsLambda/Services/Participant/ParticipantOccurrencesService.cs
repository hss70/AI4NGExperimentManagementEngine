using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGExperimentsLambda.Interfaces.Participant;
using AI4NGExperimentsLambda.Models;
using AI4NGExperimentsLambda.Models.Dtos;
using AI4NGExperimentsLambda.Models.Requests;
using AI4NGExperimentsLambda.Mappers;
using AI4NGExperimentsLambda.Models.Dtos.Responses;
using AI4NGExperimentsLambda.Models.Requests.Participant;
using AI4NGExperimentsLambda.Models.Constants;

namespace AI4NGExperimentsLambda.Services.Participant;

public sealed class ParticipantSessionOccurrencesService : IParticipantSessionOccurrencesService
{
    private const string ExperimentPkPrefix = "EXPERIMENT#";
    private const string MemberSkPrefix = "MEMBER#";
    private const string MetadataSk = "METADATA";

    private readonly IAmazonDynamoDB _dynamo;
    private readonly string _experimentsTable;

    public ParticipantSessionOccurrencesService(IAmazonDynamoDB dynamo)
    {
        _dynamo = dynamo ?? throw new ArgumentNullException(nameof(dynamo));

        _experimentsTable = Environment.GetEnvironmentVariable("EXPERIMENTS_TABLE") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_experimentsTable))
            throw new InvalidOperationException("EXPERIMENTS_TABLE environment variable is not set");
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

    public async Task<ResolveOccurrenceDto> ResolveCurrentOccurrenceAsync(
        string experimentId,
        string participantId,
        CancellationToken ct = default)
    {
        experimentId = Guard.RequireExperimentId(experimentId);
        participantId = Guard.RequireParticipantId(participantId);

        await EnsureParticipantMembershipAsync(experimentId, participantId, ct);

        // 1. Resume unfinished occurrence first
        var existing = await ListOccurrencesAsync(experimentId, participantId, ct: ct);
        var inProgress = existing
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefault(x => string.Equals(x.Status, OccurrenceStatuses.InProgress, StringComparison.OrdinalIgnoreCase));

        if (inProgress != null)
        {
            return new ResolveOccurrenceDto
            {
                ResolutionType = "resume_existing",
                Occurrence = inProgress
            };
        }

        // 2. TODO: resolve/generate required protocol occurrence based on membership + protocol rules
        // For now return available optional actions
        return new ResolveOccurrenceDto
        {
            ResolutionType = "none_available",
            AvailableActions = new List<OccurrenceActionDto>
            {
                new()
                {
                    ActionKey = "start-freeplay",
                    Label = "Start free play",
                    OccurrenceType = OccurrenceTypes.FreePlay,
                    SessionTypeKey = "FREEPLAY",
                    IsRequired = false
                }
            }
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
                ["PK"] = new AttributeValue { S = $"{ExperimentPkPrefix}{experimentId}" },
                ["SK"] = new AttributeValue { S = $"{MemberSkPrefix}{participantId}" }
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
                ["PK"] = new AttributeValue { S = $"{ExperimentPkPrefix}{experimentId}" },
                ["SK"] = new AttributeValue { S = MetadataSk }
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
    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}