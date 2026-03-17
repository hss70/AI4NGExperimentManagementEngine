using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGExperimentsLambda.Interfaces.Participant;
using AI4NGExperimentsLambda.Mappers;
using AI4NGExperimentsLambda.Models;
using AI4NGExperimentsLambda.Models.Dtos;
using AI4NGExperimentManagement.Shared;
using AI4NGExperimentsLambda.Models.Constants;

namespace AI4NGExperimentsLambda.Services.Participant;

public sealed class ParticipantExperimentsService : IParticipantExperimentsService
{
    private readonly IAmazonDynamoDB _dynamo;
    private readonly string _experimentsTable;

    public ParticipantExperimentsService(IAmazonDynamoDB dynamo)
    {
        _dynamo = dynamo;
        _experimentsTable = Environment.GetEnvironmentVariable("EXPERIMENTS_TABLE") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(_experimentsTable))
            throw new InvalidOperationException("EXPERIMENTS_TABLE environment variable is not set");
    }

    public async Task<IEnumerable<ExperimentListDto>> GetMyExperimentsAsync(
        string participantId,
        CancellationToken ct = default)
    {
        participantId = Guard.RequireParticipantId(participantId);

        var membershipResp = await _dynamo.QueryAsync(new QueryRequest
        {
            TableName = _experimentsTable,
            IndexName = "GSI1",
            KeyConditionExpression = "GSI1PK = :gsi1pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":gsi1pk"] = new AttributeValue { S = $"{DynamoTableKeys.UserPkPrefix}{participantId}" }
            }
        }, ct);

        if (membershipResp.Items.Count == 0)
            return [];

        var experimentIds = membershipResp.Items
            .Where(ExperimentMemberItemMapper.IsActiveParticipantMembership)
            .Select(ExperimentMemberItemMapper.GetExperimentId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (experimentIds.Count == 0)
            return [];

        var experiments = new List<ExperimentListDto>(experimentIds.Count);

        foreach (var experimentId in experimentIds)
        {
            var experiment = await GetExperimentAsync(experimentId, ct);
            if (experiment == null)
                continue;

            experiments.Add(new ExperimentListDto
            {
                Id = experiment.Id,
                Status = experiment.Status,
                Name = experiment.Data?.Name ?? string.Empty,
                Description = experiment.Data?.Description ?? string.Empty,
                CreatedAt = experiment.CreatedAt,
                CreatedBy = experiment.CreatedBy,
                UpdatedAt = experiment.UpdatedAt,
                UpdatedBy = experiment.UpdatedBy
            });
        }

        return experiments
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .ToList();
    }

    public async Task<ExperimentSyncDto> GetExperimentBundleAsync(
        string experimentId,
        string participantId,
        DateTime? since = null,
        CancellationToken ct = default)
    {
        experimentId = Guard.RequireExperimentId(experimentId);
        participantId = Guard.RequireParticipantId(participantId);

        await EnsureParticipantMembershipAsync(experimentId, participantId, ct);

        var experiment = await GetExperimentAsync(experimentId, ct)
            ?? throw new KeyNotFoundException($"Experiment '{experimentId}' was not found");

        var protocolSessions = await GetProtocolSessionsAsync(experimentId, ct);

        var sessions = protocolSessions
            .Select(p => ProtocolSessionItemMapper.MapProtocolSessionToSessionDto(experimentId, p, experiment))
            .ToList();

        var taskKeys = sessions
            .SelectMany(s => s.TaskOrder ?? new List<string>())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(NormaliseTaskKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var tasks = await GetTasksByKeysAsync(taskKeys, ct);

        var questionnaireIds = tasks
            .SelectMany(t => t.Data?.QuestionnaireIds ?? new List<string>())
            .Where(q => !string.IsNullOrWhiteSpace(q))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var sessionTypeNames = experiment.Data?.SessionTypes?
            .Values
            .Select(x => x.Name)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

        var sessionTypeKeys = experiment.Data?.SessionTypes?
            .Keys
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

        return new ExperimentSyncDto
        {
            Experiment = experiment,
            Sessions = sessions,
            Tasks = tasks,
            RequiredQuestionnaireIds = questionnaireIds,
            SessionNames = sessionTypeNames,
            SessionTypes = sessionTypeKeys,
            SyncTimestamp = DateTime.UtcNow.ToString("O")
        };
    }

    private async Task<ExperimentDto?> GetExperimentAsync(string experimentId, CancellationToken ct)
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

        if (!resp.IsItemSet || resp.Item == null || resp.Item.Count == 0)
            return null;

        return ExperimentItemMapper.MapExperimentDtoFromItem(resp.Item, experimentId);
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

        if (!resp.IsItemSet || resp.Item == null || resp.Item.Count == 0)
            throw new KeyNotFoundException($"Participant '{participantId}' is not enrolled in experiment '{experimentId}'");

        if (!ExperimentMemberItemMapper.IsActiveParticipantMembership(resp.Item))
            throw new UnauthorizedAccessException("Participant is not an active participant member of this experiment");
    }

    private async Task<List<ProtocolSessionDto>> GetProtocolSessionsAsync(
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

        var list = new List<ProtocolSessionDto>(resp.Items.Count);

        foreach (var item in resp.Items)
        {
            var sk = item.GetValueOrDefault("SK")?.S ?? string.Empty;
            var protocolKey = sk.StartsWith(DynamoTableKeys.ProtocolSessionSkPrefix, StringComparison.OrdinalIgnoreCase)
                ? sk.Substring(DynamoTableKeys.ProtocolSessionSkPrefix.Length)
                : sk;

            list.Add(ProtocolSessionItemMapper.MapProtocolSessionDto(experimentId, protocolKey, item));
        }

        list.Sort((a, b) => a.Order.CompareTo(b.Order));
        return list;
    }

    private async Task<List<TaskDto>> GetTasksByKeysAsync(
        IEnumerable<string> taskKeys,
        CancellationToken ct)
    {
        var keys = taskKeys
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(NormaliseTaskKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (keys.Count == 0)
            return new List<TaskDto>();

        var tasks = new List<TaskDto>(keys.Count);

        foreach (var taskKey in keys)
        {
            var resp = await _dynamo.GetItemAsync(new GetItemRequest
            {
                TableName = _experimentsTable,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = $"{DynamoTableKeys.TaskPkPrefix}{taskKey}" },
                    ["SK"] = new AttributeValue { S = DynamoTableKeys.MetadataSk }
                },
                ConsistentRead = true
            }, ct);

            if (!resp.IsItemSet || resp.Item == null || resp.Item.Count == 0)
                continue;

            if (resp.Item.TryGetValue("IsDeleted", out var isDeleted) &&
                isDeleted.BOOL.HasValue &&
                isDeleted.BOOL.Value)
            {
                continue;
            }

            var task = TaskItemMapper.MapItemToTask(resp.Item);
            tasks.Add(TaskItemMapper.MapTaskToDto(task));
        }

        return tasks;
    }

    private static string NormaliseTaskKey(string taskKey)
    {
        return (taskKey ?? string.Empty).Trim().ToUpperInvariant();
    }
}
