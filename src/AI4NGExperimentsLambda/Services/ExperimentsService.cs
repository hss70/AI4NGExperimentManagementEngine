using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGExperimentsLambda.Interfaces.Researcher;
using AI4NGExperimentsLambda.Models;
using AI4NGExperimentsLambda.Models.Dtos;
using System.Text.Json;
using AI4NGExperimentManagement.Shared;
using AI4NGExperimentsLambda.Models.Requests;

namespace AI4NGExperimentsLambda.Services;

public sealed class ExperimentsService : IExperimentsService
{
    private const string ExperimentPkPrefix = "EXPERIMENT#";
    private const string MetadataSk = "METADATA";

    private const string Gsi1Name = "GSI1";
    private const string Gsi1Pk_Experiments = "EXPERIMENT";
    private const string StatusDraft = "Draft";

    private readonly IAmazonDynamoDB _dynamo;
    private readonly string _experimentsTable;
    private readonly string _questionnairesTable;

    public ExperimentsService(IAmazonDynamoDB dynamo)
    {
        _dynamo = dynamo;

        _experimentsTable = Environment.GetEnvironmentVariable("EXPERIMENTS_TABLE") ?? string.Empty;
        var questionnairesTable = Environment.GetEnvironmentVariable("QUESTIONNAIRES_TABLE") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(_experimentsTable))
            throw new InvalidOperationException("EXPERIMENTS_TABLE environment variable is not set");

        // Optional, but Validate depends on it
        if (string.IsNullOrWhiteSpace(questionnairesTable))
            questionnairesTable = "AI4NGQuestionnaires-dev"; // keep your existing fallback

        _questionnairesTable = questionnairesTable;
    }

    public async Task<IReadOnlyList<ExperimentListDto>> GetExperimentsAsync(CancellationToken ct = default)
    {
        // Query GSI1 (NO scans)
        var resp = await _dynamo.QueryAsync(new QueryRequest
        {
            TableName = _experimentsTable,
            IndexName = Gsi1Name,
            KeyConditionExpression = "GSI1PK = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new AttributeValue { S = Gsi1Pk_Experiments }
            },
            ScanIndexForward = false, // newest first
            ProjectionExpression = "PK, #data, #status",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#data"] = "data",
                ["#status"] = "status"
            }
        }, ct);

        var list = new List<ExperimentListDto>(resp.Items.Count);

        foreach (var item in resp.Items)
        {
            var pk = item.GetValueOrDefault("PK")?.S ?? string.Empty;
            var id = pk.StartsWith(ExperimentPkPrefix, StringComparison.OrdinalIgnoreCase)
                ? pk.Substring(ExperimentPkPrefix.Length)
                : pk;
            var status = item.GetValueOrDefault("status")?.S;
            var name = string.Empty;
            var description = string.Empty;

            if (item.TryGetValue("data", out var dataAttr) && dataAttr.M != null)
            {
                var map = dataAttr.M;
                if (map.TryGetValue("Name", out var n) && !string.IsNullOrWhiteSpace(n.S))
                    name = n.S;
                if (map.TryGetValue("Description", out var d) && !string.IsNullOrWhiteSpace(d.S))
                    description = d.S;
            }

            list.Add(new ExperimentListDto
            {
                Id = id,
                Status = status,
                Name = name,
                Description = description
            });
        }

        return list;
    }

    public async Task<ExperimentDto?> GetExperimentAsync(string experimentId, CancellationToken ct = default)
    {
        experimentId = (experimentId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(experimentId))
            return null;

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
            return null;

        var response = MapExperimentDtoFromItem(resp.Item, experimentId);

        return response;
    }

    private ExperimentDto MapExperimentDtoFromItem(Dictionary<string, AttributeValue> item, string experimentId)
    {
        var status = item.GetValueOrDefault("status")?.S;
        var data = (DynamoDBHelper.ConvertAttributeValueToObject(item.GetValueOrDefault("data")) as ExperimentData) ?? new ExperimentData();

        return new ExperimentDto
        {
            Id = experimentId,
            Status = status,
            Data = data,
            UpdatedAt = item.GetValueOrDefault("updatedAt")?.S,
            UpdatedBy = item.GetValueOrDefault("updatedBy")?.S,
            CreatedAt = item.GetValueOrDefault("createdAt")?.S,
            CreatedBy = item.GetValueOrDefault("createdBy")?.S
        };
    }

    public async Task<IdResponseDto> CreateExperimentAsync(CreateExperimentRequest experiment, string performedBy, CancellationToken ct = default)
    {
        if (experiment == null)
            throw new ArgumentException("Experiment payload is required");

        performedBy = (performedBy ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(performedBy))
            throw new UnauthorizedAccessException("Authentication required");


        var nowIso = DateTime.UtcNow.ToString("O");
        var experimentId = string.IsNullOrWhiteSpace(experiment.Id)
            ? Guid.NewGuid().ToString()
            : experiment.Id.Trim();

        var data = MapAndValidateExperimentData(experiment.Data);

        var dataMap = DynamoDBHelper.JsonToAttributeValue(JsonSerializer.SerializeToElement(data));

        await _dynamo.PutItemAsync(new PutItemRequest
        {
            TableName = _experimentsTable,
            ConditionExpression = "attribute_not_exists(PK) AND attribute_not_exists(SK)",
            Item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = $"{ExperimentPkPrefix}{experimentId}" },
                ["SK"] = new AttributeValue { S = MetadataSk },

                ["type"] = new AttributeValue { S = "Experiment" },

                // GSI1 for list page
                ["GSI1PK"] = new AttributeValue { S = Gsi1Pk_Experiments },
                ["GSI1SK"] = new AttributeValue { S = nowIso },
                ["status"] = new AttributeValue { S = StatusDraft },

                ["data"] = new AttributeValue { M = dataMap },

                ["createdBy"] = new AttributeValue { S = performedBy },
                ["createdAt"] = new AttributeValue { S = nowIso },
                ["updatedBy"] = new AttributeValue { S = performedBy },
                ["updatedAt"] = new AttributeValue { S = nowIso }
            }
        }, ct);

        return new IdResponseDto { Id = experimentId };
    }

    public async Task UpdateExperimentAsync(
        string experimentId,
        UpdateExperimentRequest experiment,
        string performedBy,
        CancellationToken ct = default)
    {
        experimentId = (experimentId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(experimentId))
            throw new ArgumentException("Experiment ID is required");

        performedBy = (performedBy ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(performedBy))
            throw new UnauthorizedAccessException("Authentication required");

        if (experiment == null)
            throw new ArgumentException("Experiment data is required");

        var existing = await GetExperimentAsync(experimentId, ct);
        if (existing == null)
            throw new KeyNotFoundException($"Experiment '{experimentId}' was not found");

        var changed = false;

        var mergedStatus = existing.Status;

        var existingData = MapAndValidateExperimentData(existing.Data);
        var mergedData = existingData;

        if (experiment.Data != null)
        {
            mergedData = MapAndValidateExperimentData(existingData, experiment.Data);

            if (!ExperimentDataEquals(existingData, mergedData))
                changed = true;
        }

        if (!changed)
            return;

        var nowIso = DateTime.UtcNow.ToString("O");
        var dataMap = DynamoDBHelper.JsonToAttributeValue(JsonSerializer.SerializeToElement(mergedData));

        await _dynamo.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = $"{ExperimentPkPrefix}{experimentId}" },
                ["SK"] = new AttributeValue { S = MetadataSk }
            },
            ConditionExpression = "attribute_exists(PK) AND attribute_exists(SK)",
            UpdateExpression = "SET #data = :data, updatedBy = :u, updatedAt = :t",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#data"] = "data"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":data"] = new AttributeValue { M = dataMap },
                [":u"] = new AttributeValue { S = performedBy },
                [":t"] = new AttributeValue { S = nowIso }
            }
        }, ct);
    }
    private static ExperimentData MapAndValidateExperimentData(ExperimentData? source)
    {
        if (source == null)
            throw new ArgumentException("Experiment data is required");

        var mapped = new ExperimentData
        {
            Name = (source.Name ?? string.Empty).Trim(),
            Description = (source.Description ?? string.Empty).Trim(),
            StudyStartDate = string.IsNullOrWhiteSpace(source.StudyStartDate) ? null : source.StudyStartDate.Trim(),
            StudyEndDate = string.IsNullOrWhiteSpace(source.StudyEndDate) ? null : source.StudyEndDate.Trim(),
            ParticipantDurationDays = source.ParticipantDurationDays,
            SessionTypes = new Dictionary<string, SessionType>(StringComparer.OrdinalIgnoreCase)
        };

        foreach (var kvp in source.SessionTypes ?? new Dictionary<string, SessionType>())
        {
            var key = (kvp.Key ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Session type key cannot be empty");

            var value = kvp.Value ?? throw new ArgumentException($"Session type '{key}' is required");

            mapped.SessionTypes[key] = new SessionType
            {
                Name = (value.Name ?? string.Empty).Trim(),
                Tasks = (value.Tasks ?? new List<string>())
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Select(t => t.Trim())
                    .ToList(),
                EstimatedDurationMinutes = value.EstimatedDurationMinutes,
                Schedule = string.IsNullOrWhiteSpace(value.Schedule) ? null : value.Schedule.Trim()
            };
        }

        ValidateExperimentData(mapped);
        return mapped;
    }

    private static ExperimentData MapAndValidateExperimentData(ExperimentData existing, ExperimentDataPatch patch)
    {
        if (existing == null)
            throw new ArgumentException("Existing experiment data is required");

        if (patch == null)
            throw new ArgumentException("Experiment data patch is required");

        var merged = new ExperimentData
        {
            Name = patch.Name != null ? patch.Name.Trim() : existing.Name,
            Description = patch.Description != null ? patch.Description.Trim() : existing.Description,
            StudyStartDate = patch.StudyStartDate != null
                ? NormalizeOptionalString(patch.StudyStartDate)
                : NormalizeOptionalString(existing.StudyStartDate),
            StudyEndDate = patch.StudyEndDate != null
                ? NormalizeOptionalString(patch.StudyEndDate)
                : NormalizeOptionalString(existing.StudyEndDate),
            ParticipantDurationDays = patch.ParticipantDurationDays ?? existing.ParticipantDurationDays,
            SessionTypes = patch.SessionTypes != null
                ? MapSessionTypes(patch.SessionTypes)
                : MapSessionTypes(existing.SessionTypes)
        };

        ValidateExperimentData(merged);
        return merged;
    }

    private static Dictionary<string, SessionType> MapSessionTypes(IDictionary<string, SessionType>? source)
    {
        var mapped = new Dictionary<string, SessionType>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in source ?? new Dictionary<string, SessionType>())
        {
            var key = (kvp.Key ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Session type key cannot be empty");

            var value = kvp.Value ?? throw new ArgumentException($"Session type '{key}' is required");

            mapped[key] = new SessionType
            {
                Name = (value.Name ?? string.Empty).Trim(),
                Tasks = (value.Tasks ?? new List<string>())
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Select(t => t.Trim())
                    .ToList(),
                EstimatedDurationMinutes = value.EstimatedDurationMinutes,
                Schedule = NormalizeOptionalString(value.Schedule)
            };
        }

        return mapped;
    }

    private static string? NormalizeOptionalString(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
    private static void ValidateExperimentData(ExperimentData data)
    {
        if (string.IsNullOrWhiteSpace(data.Name))
            throw new ArgumentException("Experiment data name is required");

        if (string.IsNullOrWhiteSpace(data.Description))
            throw new ArgumentException("Experiment data description is required");

        if (data.ParticipantDurationDays.HasValue && data.ParticipantDurationDays.Value <= 0)
            throw new ArgumentException("Participant duration days must be greater than zero");

        if (data.StudyStartDate != null && !DateOnly.TryParse(data.StudyStartDate, out _))
            throw new ArgumentException("Study start date must be a valid date in YYYY-MM-DD format");

        if (data.StudyEndDate != null && !DateOnly.TryParse(data.StudyEndDate, out _))
            throw new ArgumentException("Study end date must be a valid date in YYYY-MM-DD format");

        if (data.StudyStartDate != null &&
            data.StudyEndDate != null &&
            DateOnly.Parse(data.StudyStartDate) > DateOnly.Parse(data.StudyEndDate))
        {
            throw new ArgumentException("Study start date cannot be after study end date");
        }
    }

    private static bool ExperimentDataEquals(ExperimentData left, ExperimentData right)
    {
        return JsonSerializer.Serialize(left) == JsonSerializer.Serialize(right);
    }
    public async Task DeleteExperimentAsync(string experimentId, string performedBy, CancellationToken ct = default)
    {
        experimentId = (experimentId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(experimentId))
            throw new ArgumentException("Experiment ID is required");

        performedBy = (performedBy ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(performedBy))
            throw new UnauthorizedAccessException("Authentication required");

        // For now: hard delete of metadata only (consistent with your current behaviour).
        // If you introduce syncMetadata/isDeleted later, switch to soft delete.
        await _dynamo.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = $"{ExperimentPkPrefix}{experimentId}" },
                ["SK"] = new AttributeValue { S = MetadataSk }
            },
            ConditionExpression = "attribute_exists(PK) AND attribute_exists(SK)"
        }, ct);
    }

    public Task ActivateExperimentAsync(string experimentId, string performedBy, CancellationToken ct = default)
        => SetExperimentStatusAsync(experimentId, performedBy, "Active", allowedFrom: new[] { "Draft", "Paused" }, ct);

    public Task PauseExperimentAsync(string experimentId, string performedBy, CancellationToken ct = default)
        => SetExperimentStatusAsync(experimentId, performedBy, "Paused", allowedFrom: new[] { "Active" }, ct);

    public Task CloseExperimentAsync(string experimentId, string performedBy, CancellationToken ct = default)
        => SetExperimentStatusAsync(experimentId, performedBy, "Closed", allowedFrom: new[] { "Active", "Paused" }, ct);

    private async Task SetExperimentStatusAsync(
        string experimentId,
        string performedBy,
        string targetStatus,
        IEnumerable<string> allowedFrom,
        CancellationToken ct)
    {
        experimentId = (experimentId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(experimentId))
            throw new ArgumentException("Experiment ID is required");

        performedBy = (performedBy ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(performedBy))
            throw new UnauthorizedAccessException("Authentication required");

        var nowIso = DateTime.UtcNow.ToString("O");
        var allowed = allowedFrom.ToList();

        // We store status inside data.Status (ExperimentData)
        // Canonical status is top-level "status", but allow legacy reads from data.Status for transitions.
        var allowedChecks = string.Join(" OR ", allowed.Select((_, i) => $"#status = :from{i} OR #data.#legacyStatus = :from{i}"));

        var exprAttrNames = new Dictionary<string, string>
        {
            ["#status"] = "status",
            ["#data"] = "data",
            ["#legacyStatus"] = "Status"
        };

        var exprAttrValues = new Dictionary<string, AttributeValue>
        {
            [":to"] = new AttributeValue { S = targetStatus },
            [":u"] = new AttributeValue { S = performedBy },
            [":t"] = new AttributeValue { S = nowIso }
        };

        var idx = 0;
        foreach (var s in allowed)
        {
            exprAttrValues[$":from{idx}"] = new AttributeValue { S = s };
            idx++;
        }

        await _dynamo.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = $"{ExperimentPkPrefix}{experimentId}" },
                ["SK"] = new AttributeValue { S = MetadataSk }
            },
            ConditionExpression = $"attribute_exists(PK) AND attribute_exists(SK) AND ({allowedChecks})",
            UpdateExpression = "SET #status = :to, updatedBy = :u, updatedAt = :t",
            ExpressionAttributeNames = exprAttrNames,
            ExpressionAttributeValues = exprAttrValues
        }, ct);
    }
}
