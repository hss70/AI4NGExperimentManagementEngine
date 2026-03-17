using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGExperimentsLambda.Interfaces.Researcher;
using AI4NGExperimentsLambda.Models;
using AI4NGExperimentsLambda.Models.Dtos;
using AI4NGExperimentsLambda.Models.Requests;
using System.Text.Json;
using AI4NGExperimentsLambda.Mappers;
using AI4NGExperimentsLambda.Models.Constants;

namespace AI4NGExperimentsLambda.Services.Researcher;

public sealed class ExperimentsService : IExperimentsService
{
    private const string Gsi1Name = "GSI1";
    private const string StatusDraft = "Draft";

    private readonly IAmazonDynamoDB _dynamo;
    private readonly string _experimentsTable;

    public ExperimentsService(IAmazonDynamoDB dynamo)
    {
        _dynamo = dynamo ?? throw new ArgumentNullException(nameof(dynamo));

        _experimentsTable = Environment.GetEnvironmentVariable("EXPERIMENTS_TABLE") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(_experimentsTable))
            throw new InvalidOperationException("EXPERIMENTS_TABLE environment variable is not set");
    }

    public async Task<IReadOnlyList<ExperimentListDto>> GetExperimentsAsync(CancellationToken ct = default)
    {
        var resp = await _dynamo.QueryAsync(new QueryRequest
        {
            TableName = _experimentsTable,
            IndexName = Gsi1Name,
            KeyConditionExpression = "GSI1PK = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new AttributeValue { S = DynamoTableKeys.ExperimentGsiPk }
            },
            ScanIndexForward = false,
            ProjectionExpression = "PK, #data, #status, createdBy, createdAt, updatedBy, updatedAt",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#data"] = "data",
                ["#status"] = "status"
            }
        }, ct);

        var list = new List<ExperimentListDto>(resp.Items.Count);

        foreach (var item in resp.Items)
        {
            var experimentId = ExtractExperimentId(item);
            var data = ExperimentItemMapper.MapExperimentDataFromItem(item);

            list.Add(new ExperimentListDto
            {
                Id = experimentId,
                Status = item.GetValueOrDefault("status")?.S ?? string.Empty,
                Name = data.Name,
                Description = data.Description,
                UpdatedAt = item.GetValueOrDefault("updatedAt")?.S,
                UpdatedBy = item.GetValueOrDefault("updatedBy")?.S,
                CreatedAt = item.GetValueOrDefault("createdAt")?.S,
                CreatedBy = item.GetValueOrDefault("createdBy")?.S ?? string.Empty
            });
        }

        return list;
    }

    public async Task<ExperimentDto?> GetExperimentAsync(string experimentId, CancellationToken ct = default)
    {
        experimentId = NormalizeRequired(experimentId, "Experiment ID is required");

        var resp = await _dynamo.GetItemAsync(new GetItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = BuildExperimentPk(experimentId) },
                ["SK"] = new AttributeValue { S = DynamoTableKeys.MetadataSk }
            },
            ConsistentRead = true
        }, ct);

        if (!resp.IsItemSet)
            return null;

        return ExperimentItemMapper.MapExperimentDtoFromItem(resp.Item, experimentId);
    }

    public async Task<IdResponseDto> CreateExperimentAsync(
        CreateExperimentRequest experiment,
        string performedBy,
        CancellationToken ct = default)
    {
        if (experiment == null)
            throw new ArgumentException("Experiment payload is required");

        performedBy = NormalizeRequired(performedBy, "Authentication required");

        var nowIso = DateTime.UtcNow.ToString("O");
        var experimentId = string.IsNullOrWhiteSpace(experiment.Id)
            ? Guid.NewGuid().ToString()
            : experiment.Id.Trim();

        var data = MapAndValidateExperimentData(experiment.Data);
        var dataMap = MapExperimentDataToAttributeMap(data);

        await _dynamo.PutItemAsync(new PutItemRequest
        {
            TableName = _experimentsTable,
            ConditionExpression = "attribute_not_exists(PK) AND attribute_not_exists(SK)",
            Item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = BuildExperimentPk(experimentId) },
                ["SK"] = new AttributeValue { S = DynamoTableKeys.MetadataSk },

                ["type"] = new AttributeValue { S = "Experiment" },
                ["GSI1PK"] = new AttributeValue { S = DynamoTableKeys.ExperimentGsiPk },
                ["GSI1SK"] = new AttributeValue { S = nowIso },
                ["status"] = new AttributeValue { S = StatusDraft },
                ["data"] = new AttributeValue { M = dataMap },

                ["createdBy"] = new AttributeValue { S = performedBy },
                ["createdAt"] = new AttributeValue { S = nowIso },
                ["updatedBy"] = new AttributeValue { S = performedBy },
                ["updatedAt"] = new AttributeValue { S = nowIso }
            }
        }, ct);

        return new IdResponseDto
        {
            Id = experimentId
        };
    }

    public async Task UpdateExperimentAsync(
        string experimentId,
        UpdateExperimentRequest experiment,
        string performedBy,
        CancellationToken ct = default)
    {
        experimentId = NormalizeRequired(experimentId, "Experiment ID is required");
        performedBy = NormalizeRequired(performedBy, "Authentication required");

        if (experiment == null)
            throw new ArgumentException("Experiment data is required");

        var existing = await GetExperimentAsync(experimentId, ct);
        if (existing == null)
            throw new KeyNotFoundException($"Experiment '{experimentId}' was not found");

        var changed = false;

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
        var dataMap = MapExperimentDataToAttributeMap(mergedData);

        await _dynamo.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = BuildExperimentPk(experimentId) },
                ["SK"] = new AttributeValue { S = DynamoTableKeys.MetadataSk }
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

    public async Task DeleteExperimentAsync(string experimentId, string performedBy, CancellationToken ct = default)
    {
        experimentId = NormalizeRequired(experimentId, "Experiment ID is required");
        performedBy = NormalizeRequired(performedBy, "Authentication required");

        await _dynamo.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = BuildExperimentPk(experimentId) },
                ["SK"] = new AttributeValue { S = DynamoTableKeys.MetadataSk }
            },
            ConditionExpression = "attribute_exists(PK) AND attribute_exists(SK)"
        }, ct);
    }

    public Task ActivateExperimentAsync(string experimentId, string performedBy, CancellationToken ct = default)
        => SetExperimentStatusAsync(experimentId, performedBy, "Active", new[] { "Draft", "Paused" }, ct);

    public Task PauseExperimentAsync(string experimentId, string performedBy, CancellationToken ct = default)
        => SetExperimentStatusAsync(experimentId, performedBy, "Paused", new[] { "Active" }, ct);

    public Task CloseExperimentAsync(string experimentId, string performedBy, CancellationToken ct = default)
        => SetExperimentStatusAsync(experimentId, performedBy, "Closed", new[] { "Active", "Paused" }, ct);

    private async Task SetExperimentStatusAsync(
        string experimentId,
        string performedBy,
        string targetStatus,
        IEnumerable<string> allowedFrom,
        CancellationToken ct)
    {
        experimentId = NormalizeRequired(experimentId, "Experiment ID is required");
        performedBy = NormalizeRequired(performedBy, "Authentication required");

        var nowIso = DateTime.UtcNow.ToString("O");
        var allowed = allowedFrom.ToList();

        var allowedChecks = string.Join(" OR ", allowed.Select((_, i) => $"#status = :from{i}"));

        var exprAttrNames = new Dictionary<string, string>
        {
            ["#status"] = "status"
        };

        var exprAttrValues = new Dictionary<string, AttributeValue>
        {
            [":to"] = new AttributeValue { S = targetStatus },
            [":u"] = new AttributeValue { S = performedBy },
            [":t"] = new AttributeValue { S = nowIso }
        };

        for (var i = 0; i < allowed.Count; i++)
        {
            exprAttrValues[$":from{i}"] = new AttributeValue { S = allowed[i] };
        }

        await _dynamo.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = BuildExperimentPk(experimentId) },
                ["SK"] = new AttributeValue { S = DynamoTableKeys.MetadataSk }
            },
            ConditionExpression = $"attribute_exists(PK) AND attribute_exists(SK) AND ({allowedChecks})",
            UpdateExpression = "SET #status = :to, updatedBy = :u, updatedAt = :t",
            ExpressionAttributeNames = exprAttrNames,
            ExpressionAttributeValues = exprAttrValues
        }, ct);
    }

    private static string BuildExperimentPk(string experimentId)
        => $"{DynamoTableKeys.ExperimentPkPrefix}{experimentId}";

    private static string NormalizeRequired(string? value, string errorMessage)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException(errorMessage);

        return trimmed;
    }

    private static string ExtractExperimentId(Dictionary<string, AttributeValue> item)
    {
        var pk = item.GetValueOrDefault("PK")?.S ?? string.Empty;

        return pk.StartsWith(DynamoTableKeys.ExperimentPkPrefix, StringComparison.OrdinalIgnoreCase)
            ? pk.Substring(DynamoTableKeys.ExperimentPkPrefix.Length)
            : pk;
    }

    private static Dictionary<string, AttributeValue> MapExperimentDataToAttributeMap(ExperimentData data)
    {
        var map = new Dictionary<string, AttributeValue>
        {
            ["Name"] = new AttributeValue { S = data.Name ?? string.Empty },
            ["Description"] = new AttributeValue { S = data.Description ?? string.Empty }
        };

        if (!string.IsNullOrWhiteSpace(data.StudyStartDate))
            map["StudyStartDate"] = new AttributeValue { S = data.StudyStartDate };

        if (!string.IsNullOrWhiteSpace(data.StudyEndDate))
            map["StudyEndDate"] = new AttributeValue { S = data.StudyEndDate };

        if (data.ParticipantDurationDays.HasValue)
            map["ParticipantDurationDays"] = new AttributeValue { N = data.ParticipantDurationDays.Value.ToString() };

        var sessionTypesMap = new Dictionary<string, AttributeValue>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in data.SessionTypes ?? new Dictionary<string, SessionType>())
        {
            var key = (kvp.Key ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(key))
                continue;

            var value = kvp.Value ?? new SessionType();

            var sessionMap = new Dictionary<string, AttributeValue>
            {
                ["Name"] = new AttributeValue { S = value.Name ?? string.Empty },
                ["Description"] = new AttributeValue { S = value.Description ?? string.Empty },
                ["EstimatedDurationMinutes"] = new AttributeValue { N = value.EstimatedDurationMinutes.ToString() },
                ["Tasks"] = new AttributeValue
                {
                    L = (value.Tasks ?? new List<string>())
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .Select(t => new AttributeValue { S = t.Trim() })
                        .ToList()
                }
            };

            if (!string.IsNullOrWhiteSpace(value.Schedule))
                sessionMap["Schedule"] = new AttributeValue { S = value.Schedule };

            sessionTypesMap[key] = new AttributeValue { M = sessionMap };
        }

        map["SessionTypes"] = new AttributeValue { M = sessionTypesMap };

        return map;
    }

    private static ExperimentData MapAndValidateExperimentData(ExperimentData? source)
    {
        if (source == null)
            throw new ArgumentException("Experiment data is required");

        var mapped = new ExperimentData
        {
            Name = (source.Name ?? string.Empty).Trim(),
            Description = (source.Description ?? string.Empty).Trim(),
            StudyStartDate = NormalizeOptionalString(source.StudyStartDate),
            StudyEndDate = NormalizeOptionalString(source.StudyEndDate),
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
                Description = (value.Description ?? string.Empty).Trim(),
                Tasks = (value.Tasks ?? new List<string>())
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Select(t => t.Trim())
                    .ToList(),
                EstimatedDurationMinutes = value.EstimatedDurationMinutes,
                Schedule = NormalizeOptionalString(value.Schedule)
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
                Description = (value.Description ?? string.Empty).Trim(),
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
}
