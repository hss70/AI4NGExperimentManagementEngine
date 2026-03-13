using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGExperimentsLambda.Interfaces.Researcher;
using AI4NGExperimentsLambda.Models;
using AI4NGExperimentsLambda.Models.Dtos;
using AI4NGExperimentsLambda.Models.Helpers;

namespace AI4NGExperimentsLambda.Services;

public sealed class ExperimentParticipantsService : IExperimentParticipantsService
{
    private const string ExperimentPkPrefix = "EXPERIMENT#";
    private const string MetadataSk = "METADATA";
    private const string MemberSkPrefix = "MEMBER#";

    private const string MembershipType = "Membership";

    private static readonly HashSet<string> AllowedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "participant",
        "researcher"
    };

    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "active",
        "paused",
        "withdrawn",
        "completed"
    };

    private readonly IAmazonDynamoDB _dynamo;
    private readonly string _experimentsTable;

    public ExperimentParticipantsService(IAmazonDynamoDB dynamo)
    {
        _dynamo = dynamo;
        _experimentsTable = Environment.GetEnvironmentVariable("EXPERIMENTS_TABLE") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(_experimentsTable))
            throw new InvalidOperationException("EXPERIMENTS_TABLE environment variable is not set");
    }

    public async Task<IEnumerable<ExperimentMemberDto>> GetExperimentParticipantsAsync(
        string experimentId,
        string? cohort = null,
        string? status = null,
        string? role = null,
        CancellationToken ct = default)
    {
        experimentId = RequireExperimentId(experimentId);

        var resp = await _dynamo.QueryAsync(new QueryRequest
        {
            TableName = _experimentsTable,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :skPrefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new AttributeValue { S = $"{ExperimentPkPrefix}{experimentId}" },
                [":skPrefix"] = new AttributeValue { S = MemberSkPrefix }
            }
        }, ct);

        var members = resp.Items.Select(ExperimentMemberItemMapper.MapMemberDto);

        if (!string.IsNullOrWhiteSpace(cohort))
            members = members.Where(x => string.Equals(x.Cohort, cohort.Trim(), StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(status))
            members = members.Where(x => string.Equals(x.Status, status.Trim(), StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(role))
            members = members.Where(x => string.Equals(x.Role, role.Trim(), StringComparison.OrdinalIgnoreCase));

        return members.ToList();
    }

    public async Task<IdResponseDto> UpsertParticipantAsync(
        string experimentId,
        string participantId,
        ExperimentMemberRequest request,
        string performedBy,
        CancellationToken ct = default)
    {
        experimentId = RequireExperimentId(experimentId);
        participantId = RequireParticipantId(participantId);
        performedBy = RequirePerformedBy(performedBy);

        if (request == null)
            throw new ArgumentException("Participant request is required");

        await EnsureExperimentExistsAsync(experimentId, ct);

        var normalised = NormaliseRequest(request);
        var nowIso = DateTime.UtcNow.ToString("O");

        await _dynamo.PutItemAsync(new PutItemRequest
        {
            TableName = _experimentsTable,
            Item = BuildMembershipItem(experimentId, participantId, normalised, performedBy, nowIso),
        }, ct);

        return new IdResponseDto { Id = participantId };
    }

    public async Task<List<IdResponseDto>> UpsertParticipantsBatchAsync(
        string experimentId,
        IEnumerable<MemberBatchItem> participants,
        string performedBy,
        CancellationToken ct = default)
    {
        var responses = new List<IdResponseDto>();
        experimentId = RequireExperimentId(experimentId);
        performedBy = RequirePerformedBy(performedBy);

        if (participants == null)
            throw new ArgumentException("Participants payload is required");

        await EnsureExperimentExistsAsync(experimentId, ct);

        var items = participants.ToList();
        if (items.Count == 0)
            return responses;

        var nowIso = DateTime.UtcNow.ToString("O");

        // Dynamo batch write limit is 25
        foreach (var chunk in Chunk(items, 25))
        {
            var writes = new List<WriteRequest>(chunk.Count);
            var chunkParticipantIds = new List<string>(chunk.Count);

            foreach (var participant in chunk)
            {
                var participantId = RequireParticipantId(participant.UserSub);
                chunkParticipantIds.Add(participantId);

                var request = new ExperimentMemberRequest
                {
                    Role = participant.Role,
                    Status = participant.Status,
                    Cohort = participant.Cohort,
                    ParticipantStartDate = participant.ParticipantStartDate,
                    ParticipantEndDate = participant.ParticipantEndDate,
                    ParticipantDurationDaysOverride = participant.ParticipantDurationDaysOverride,
                    Timezone = participant.Timezone,
                    PseudoId = participant.PseudoId
                };

                var normalised = NormaliseRequest(request);

                writes.Add(new WriteRequest
                {
                    PutRequest = new PutRequest
                    {
                        Item = BuildMembershipItem(experimentId, participantId, normalised, performedBy, nowIso)
                    }
                });
            }

            var batchRequest = new BatchWriteItemRequest
            {
                RequestItems = new Dictionary<string, List<WriteRequest>>
                {
                    [_experimentsTable] = writes
                }
            };

            var response = await _dynamo.BatchWriteItemAsync(batchRequest, ct);

            // Minimal retry for unprocessed items
            var unprocessed = response.UnprocessedItems;
            var retryCount = 0;

            while (unprocessed != null && unprocessed.Count > 0 && retryCount < 3)
            {
                response = await _dynamo.BatchWriteItemAsync(new BatchWriteItemRequest
                {
                    RequestItems = unprocessed
                }, ct);

                unprocessed = response.UnprocessedItems;
                retryCount++;
            }

            if (unprocessed != null && unprocessed.Count > 0)
                throw new InvalidOperationException("Some participant memberships could not be written after retries");

            responses.AddRange(chunkParticipantIds.Select(id => new IdResponseDto
            {
                Id = id
            }));
        }

        return responses;
    }

    public async Task RemoveParticipantAsync(
        string experimentId,
        string participantId,
        string performedBy,
        CancellationToken ct = default)
    {
        experimentId = RequireExperimentId(experimentId);
        participantId = RequireParticipantId(participantId);
        performedBy = RequirePerformedBy(performedBy);

        await _dynamo.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = $"{ExperimentPkPrefix}{experimentId}" },
                ["SK"] = new AttributeValue { S = $"{MemberSkPrefix}{participantId}" }
            },
            ConditionExpression = "attribute_exists(PK) AND attribute_exists(SK)"
        }, ct);
    }

    private async Task EnsureExperimentExistsAsync(string experimentId, CancellationToken ct)
    {
        var resp = await _dynamo.GetItemAsync(new GetItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = $"{ExperimentPkPrefix}{experimentId}" },
                ["SK"] = new AttributeValue { S = MetadataSk }
            },
            ConsistentRead = true,
            ProjectionExpression = "PK"
        }, ct);

        if (!resp.IsItemSet)
            throw new KeyNotFoundException($"Experiment '{experimentId}' was not found");
    }

    private static Dictionary<string, AttributeValue> BuildMembershipItem(
        string experimentId,
        string participantId,
        ExperimentMemberRequest request,
        string performedBy,
        string nowIso)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = $"{ExperimentPkPrefix}{experimentId}" },
            ["SK"] = new AttributeValue { S = $"{MemberSkPrefix}{participantId}" },

            ["GSI1PK"] = new AttributeValue { S = $"USER#{participantId}" },
            ["GSI1SK"] = new AttributeValue { S = $"EXPERIMENT#{experimentId}" },

            ["type"] = new AttributeValue { S = MembershipType },
            ["role"] = new AttributeValue { S = request.Role },
            ["status"] = new AttributeValue { S = request.Status },
            ["cohort"] = new AttributeValue { S = request.Cohort },

            ["createdBy"] = new AttributeValue { S = performedBy },
            ["createdAt"] = new AttributeValue { S = nowIso },
            ["updatedBy"] = new AttributeValue { S = performedBy },
            ["updatedAt"] = new AttributeValue { S = nowIso }
        };

        AddOptionalString(item, "participantStartDate", request.ParticipantStartDate);
        AddOptionalString(item, "participantEndDate", request.ParticipantEndDate);
        AddOptionalNumber(item, "participantDurationDaysOverride", request.ParticipantDurationDaysOverride);
        AddOptionalString(item, "timezone", request.Timezone);
        AddOptionalString(item, "pseudoId", request.PseudoId);

        return item;
    }

    private static ExperimentMemberRequest NormaliseRequest(ExperimentMemberRequest request)
    {
        var role = string.IsNullOrWhiteSpace(request.Role) ? "participant" : request.Role.Trim().ToLowerInvariant();
        var status = string.IsNullOrWhiteSpace(request.Status) ? "active" : request.Status.Trim().ToLowerInvariant();
        var cohort = (request.Cohort ?? string.Empty).Trim();

        if (!AllowedRoles.Contains(role))
            throw new ArgumentException($"Invalid role '{request.Role}'");

        if (!AllowedStatuses.Contains(status))
            throw new ArgumentException($"Invalid status '{request.Status}'");

        if (request.ParticipantDurationDaysOverride.HasValue && request.ParticipantDurationDaysOverride.Value <= 0)
            throw new ArgumentException("ParticipantDurationDaysOverride must be greater than 0 when provided");

        return new ExperimentMemberRequest
        {
            Role = role,
            Status = status,
            Cohort = cohort,
            ParticipantStartDate = CleanOptional(request.ParticipantStartDate),
            ParticipantEndDate = CleanOptional(request.ParticipantEndDate),
            ParticipantDurationDaysOverride = request.ParticipantDurationDaysOverride,
            Timezone = CleanOptional(request.Timezone),
            PseudoId = CleanOptional(request.PseudoId)
        };
    }
    private static string RequireExperimentId(string experimentId)
    {
        experimentId = (experimentId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(experimentId))
            throw new ArgumentException("Experiment ID is required");
        return experimentId;
    }

    private static string RequireParticipantId(string participantId)
    {
        participantId = (participantId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(participantId))
            throw new ArgumentException("Participant ID is required");
        return participantId;
    }

    private static string RequirePerformedBy(string performedBy)
    {
        performedBy = (performedBy ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(performedBy))
            throw new UnauthorizedAccessException("Authentication required");
        return performedBy;
    }

    private static string? CleanOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim();
    }

    private static void AddOptionalString(Dictionary<string, AttributeValue> item, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            item[key] = new AttributeValue { S = value.Trim() };
    }

    private static void AddOptionalNumber(Dictionary<string, AttributeValue> item, string key, int? value)
    {
        if (value.HasValue)
            item[key] = new AttributeValue { N = value.Value.ToString() };
    }

    private static int? TryParseInt(string? value)
    {
        return int.TryParse(value, out var result) ? result : null;
    }

    private static List<List<T>> Chunk<T>(List<T> source, int size)
    {
        var chunks = new List<List<T>>();
        for (var i = 0; i < source.Count; i += size)
            chunks.Add(source.GetRange(i, Math.Min(size, source.Count - i)));

        return chunks;
    }

    public async Task<ExperimentMemberDto?> GetExperimentParticipantAsync(
        string experimentId,
        string participantId,
        CancellationToken ct = default)
    {
        experimentId = RequireExperimentId(experimentId);
        participantId = RequireParticipantId(participantId);

        var response = await _dynamo.GetItemAsync(new GetItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = $"{ExperimentPkPrefix}{experimentId}" },
                ["SK"] = new AttributeValue { S = $"{MemberSkPrefix}{participantId}" }
            },
            ConsistentRead = true
        }, ct);

        if (!response.IsItemSet || response.Item == null || response.Item.Count == 0)
            return null;

        return ExperimentMemberItemMapper.MapMemberDto(response.Item);
    }
}