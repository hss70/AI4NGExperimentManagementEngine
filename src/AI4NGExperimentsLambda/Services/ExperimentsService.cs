using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGExperimentsLambda.Interfaces.Researcher;
using AI4NGExperimentsLambda.Models;
using AI4NGExperimentsLambda.Models.Dtos;
using System.Text.Json;
using AI4NGExperimentManagement.Shared;

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
                Status = ResolveStatus(item),
                Name = name,
                Description = description
            });
        }

        return list;
    }

    public async Task<ExperimentDto?> GetExperimentAsync(string experimentId, CancellationToken ct = default)
    {
        experimentId = ( experimentId ?? string.Empty ).Trim();
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

        var item = resp.Item;
        var data = ( DynamoDBHelper.ConvertAttributeValueToObject(item.GetValueOrDefault("data")) as ExperimentData ) ?? new ExperimentData();

        return new ExperimentDto
        {
            Id = experimentId,
            Status = ResolveStatus(item, data),
            Data = data,
            QuestionnaireConfig = ( DynamoDBHelper.ConvertAttributeValueToObject(item.GetValueOrDefault("questionnaireConfig")) as QuestionnaireConfig ) ?? new QuestionnaireConfig(),
            UpdatedAt = item.GetValueOrDefault("updatedAt")?.S
        };
    }

    public async Task<ValidateExperimentResponseDto> ValidateExperimentAsync(Experiment experiment, CancellationToken ct = default)
    {
        if (experiment == null)
            throw new ArgumentException("Experiment payload is required");

        var questionnaireIds = CollectQuestionnaireIds(experiment);
        var missing = await ValidateQuestionnaires(questionnaireIds, ct);

        return new ValidateExperimentResponseDto
        {
            Valid = missing.Count == 0,
            ReferencedQuestionnaires = questionnaireIds.ToList(),
            MissingQuestionnaires = missing,
            Message = missing.Count > 0
                ? $"Missing questionnaires: {string.Join(", ", missing)}"
                : "All dependencies are valid"
        };
    }

    public async Task<IdResponseDto> CreateExperimentAsync(Experiment experiment, string performedBy, CancellationToken ct = default)
    {
        if (experiment == null)
            throw new ArgumentException("Experiment payload is required");

        performedBy = ( performedBy ?? string.Empty ).Trim();
        if (string.IsNullOrWhiteSpace(performedBy))
            throw new UnauthorizedAccessException("Authentication required");

        // Validate deps
        var questionnaireIds = CollectQuestionnaireIds(experiment);
        var missing = await ValidateQuestionnaires(questionnaireIds, ct);
        if (missing.Count > 0)
            throw new ArgumentException($"Missing questionnaires: {string.Join(", ", missing)}");

        var nowIso = DateTime.UtcNow.ToString("O");
        var experimentId = string.IsNullOrWhiteSpace(experiment.Id)
            ? Guid.NewGuid().ToString()
            : experiment.Id.Trim();

        var data = experiment.Data ?? new ExperimentData();

        // Normalise data.questionnaireIds
        var dataMap = DynamoDBHelper.JsonToAttributeValue(JsonSerializer.SerializeToElement(data));
        dataMap["questionnaireIds"] = new AttributeValue
        {
            L = questionnaireIds.Select(q => new AttributeValue { S = q }).ToList()
        };

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
                ["questionnaireConfig"] = new AttributeValue
                {
                    M = DynamoDBHelper.JsonToAttributeValue(JsonSerializer.SerializeToElement(experiment.QuestionnaireConfig))
                },

                ["createdBy"] = new AttributeValue { S = performedBy },
                ["createdAt"] = new AttributeValue { S = nowIso },
                ["updatedBy"] = new AttributeValue { S = performedBy },
                ["updatedAt"] = new AttributeValue { S = nowIso }
            }
        }, ct);

        return new IdResponseDto { Id = experimentId };
    }

    public async Task UpdateExperimentAsync(string experimentId, ExperimentData data, string performedBy, CancellationToken ct = default)
    {
        experimentId = ( experimentId ?? string.Empty ).Trim();
        if (string.IsNullOrWhiteSpace(experimentId))
            throw new ArgumentException("Experiment ID is required");

        performedBy = ( performedBy ?? string.Empty ).Trim();
        if (string.IsNullOrWhiteSpace(performedBy))
            throw new UnauthorizedAccessException("Authentication required");

        if (data == null)
            throw new ArgumentException("Experiment data is required");

        // Normalise: keep derived questionnaireIds inside data
        var normalised = DynamoDBHelper.JsonToAttributeValue(JsonSerializer.SerializeToElement(data));
        var derived = DeriveQuestionnaireIds(data);
        var missing = await ValidateQuestionnaires(derived, ct);
        if (missing.Count > 0)
            throw new ArgumentException($"Missing questionnaires: {string.Join(", ", missing)}");

        normalised["questionnaireIds"] = new AttributeValue
        {
            L = derived.Select(q => new AttributeValue { S = q }).ToList()
        };

        var nowIso = DateTime.UtcNow.ToString("O");

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
                [":data"] = new AttributeValue { M = normalised },
                [":u"] = new AttributeValue { S = performedBy },
                [":t"] = new AttributeValue { S = nowIso }
            }
        }, ct);
    }

    public async Task DeleteExperimentAsync(string experimentId, string performedBy, CancellationToken ct = default)
    {
        experimentId = ( experimentId ?? string.Empty ).Trim();
        if (string.IsNullOrWhiteSpace(experimentId))
            throw new ArgumentException("Experiment ID is required");

        performedBy = ( performedBy ?? string.Empty ).Trim();
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
        experimentId = ( experimentId ?? string.Empty ).Trim();
        if (string.IsNullOrWhiteSpace(experimentId))
            throw new ArgumentException("Experiment ID is required");

        performedBy = ( performedBy ?? string.Empty ).Trim();
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

    private static HashSet<string> CollectQuestionnaireIds(Experiment experiment)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (experiment.Data?.SessionTypes != null)
        {
            foreach (var st in experiment.Data.SessionTypes.Values)
            {
                if (st?.Questionnaires == null)
                    continue;
                foreach (var q in st.Questionnaires)
                    if (!string.IsNullOrWhiteSpace(q))
                        ids.Add(q.Trim());
            }
        }

        if (experiment.QuestionnaireConfig?.Schedule != null)
        {
            foreach (var q in experiment.QuestionnaireConfig.Schedule.Keys)
                if (!string.IsNullOrWhiteSpace(q))
                    ids.Add(q.Trim());
        }

        return ids;
    }

    private static HashSet<string> DeriveQuestionnaireIds(ExperimentData data)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (data?.SessionTypes != null)
        {
            foreach (var st in data.SessionTypes.Values)
            {
                if (st?.Questionnaires == null)
                    continue;
                foreach (var q in st.Questionnaires)
                    if (!string.IsNullOrWhiteSpace(q))
                        ids.Add(q.Trim());
            }
        }

        return ids;
    }

    private async Task<List<string>> ValidateQuestionnaires(HashSet<string> questionnaireIds, CancellationToken ct)
    {
        var missing = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var qid in questionnaireIds)
        {
            var trimmedQid = ( qid ?? string.Empty ).Trim();
            if (string.IsNullOrWhiteSpace(trimmedQid))
                continue;
            if (!seen.Add(trimmedQid))
                continue;

            var resp = await _dynamo.GetItemAsync(new GetItemRequest
            {
                TableName = _questionnairesTable,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = $"QUESTIONNAIRE#{trimmedQid}" },
                    ["SK"] = new AttributeValue { S = "CONFIG" }
                }
            }, ct);

            if (!resp.IsItemSet)
                missing.Add(trimmedQid);
        }

        return missing;
    }

    private static string ResolveStatus(Dictionary<string, AttributeValue> item, ExperimentData? data = null)
    {
        var topLevel = item.GetValueOrDefault("status")?.S;
        if (!string.IsNullOrWhiteSpace(topLevel))
            return topLevel;

        var legacy = data?.Status;
        if (string.IsNullOrWhiteSpace(legacy) &&
            item.TryGetValue("data", out var dataAttr) &&
            dataAttr.M != null &&
            dataAttr.M.TryGetValue("Status", out var legacyAttr) &&
            !string.IsNullOrWhiteSpace(legacyAttr.S))
        {
            legacy = legacyAttr.S;
        }

        return legacy ?? string.Empty;
    }
}
