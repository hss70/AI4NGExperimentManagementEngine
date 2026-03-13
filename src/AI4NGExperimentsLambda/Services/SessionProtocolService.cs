using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGExperimentsLambda.Interfaces.Researcher;
using AI4NGExperimentsLambda.Models;
using AI4NGExperimentsLambda.Models.Dtos;
using AI4NGExperimentsLambda.Models.Requests;
using AI4NGExperimentManagement.Shared;
using System.Text.Json;
using AI4NGExperimentsLambda.Helpers;

namespace AI4NGExperimentsLambda.Services;

public sealed class SessionProtocolService : ISessionProtocolService
{
    private const string ExperimentPkPrefix = "EXPERIMENT#";
    private const string MetadataSk = "METADATA";
    private const string ProtocolSkPrefix = "PROTOCOL_SESSION#";

    private readonly IAmazonDynamoDB _dynamo;
    private readonly string _experimentsTable;

    public SessionProtocolService(IAmazonDynamoDB dynamo)
    {
        _dynamo = dynamo;
        _experimentsTable = Environment.GetEnvironmentVariable("EXPERIMENTS_TABLE") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(_experimentsTable))
            throw new InvalidOperationException("EXPERIMENTS_TABLE environment variable is not set");
    }

    public async Task<IReadOnlyList<ProtocolSessionDto>> GetProtocolSessionsAsync(
        string experimentId,
        CancellationToken ct = default)
    {
        experimentId = (experimentId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(experimentId))
            throw new ArgumentException("Experiment ID is required");

        var resp = await _dynamo.QueryAsync(new QueryRequest
        {
            TableName = _experimentsTable,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :skprefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new AttributeValue { S = $"{ExperimentPkPrefix}{experimentId}" },
                [":skprefix"] = new AttributeValue { S = ProtocolSkPrefix }
            }
        }, ct);

        var list = new List<ProtocolSessionDto>(resp.Items.Count);

        foreach (var item in resp.Items)
        {
            var sk = item.GetValueOrDefault("SK")?.S ?? string.Empty;
            var protocolKey = sk.StartsWith(ProtocolSkPrefix, StringComparison.OrdinalIgnoreCase)
                ? sk.Substring(ProtocolSkPrefix.Length)
                : sk;

            var dataObj = DynamoDBHelper.ConvertAttributeValueToObject(item.GetValueOrDefault("data"));
            list.Add(MapToDto(experimentId, protocolKey, dataObj, item));
        }

        // Stable ordering for UI
        list.Sort((a, b) => a.Order.CompareTo(b.Order));
        return list;
    }

    public async Task<ProtocolSessionDto?> GetProtocolSessionAsync(
        string experimentId,
        string protocolKey,
        CancellationToken ct = default)
    {
        experimentId = (experimentId ?? string.Empty).Trim();
        protocolKey = NormaliseKey(protocolKey);

        if (string.IsNullOrWhiteSpace(experimentId))
            throw new ArgumentException("Experiment ID is required");
        if (string.IsNullOrWhiteSpace(protocolKey))
            throw new ArgumentException("Protocol key is required");

        var resp = await _dynamo.GetItemAsync(new GetItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = $"{ExperimentPkPrefix}{experimentId}" },
                ["SK"] = new AttributeValue { S = $"{ProtocolSkPrefix}{protocolKey}" }
            },
            ConsistentRead = true
        }, ct);

        if (!resp.IsItemSet)
            return null;

        var item = resp.Item;
        var dataObj = DynamoDBHelper.ConvertAttributeValueToObject(item.GetValueOrDefault("data"));
        return MapToDto(experimentId, protocolKey, dataObj, item);
    }

    public Task<ProtocolSessionDto> CreateProtocolSessionAsync(
        string experimentId,
        string protocolKey,
        UpsertProtocolSessionRequest request,
        string performedBy,
        CancellationToken ct = default)
        => WriteProtocolSessionAsync(
            experimentId,
            protocolKey,
            request,
            performedBy,
            mode: WriteMode.Create,
            ct);

    public Task<ProtocolSessionDto> UpdateProtocolSessionAsync(
        string experimentId,
        string protocolKey,
        UpsertProtocolSessionRequest request,
        string performedBy,
        CancellationToken ct = default)
        => WriteProtocolSessionAsync(
            experimentId,
            protocolKey,
            request,
            performedBy,
            mode: WriteMode.Update,
            ct);

    public async Task DeleteProtocolSessionAsync(
        string experimentId,
        string protocolKey,
        string performedBy,
        CancellationToken ct = default)
    {
        experimentId = (experimentId ?? string.Empty).Trim();
        protocolKey = NormaliseKey(protocolKey);
        performedBy = (performedBy ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(experimentId))
            throw new ArgumentException("Experiment ID is required");
        if (string.IsNullOrWhiteSpace(protocolKey))
            throw new ArgumentException("Protocol key is required");
        if (string.IsNullOrWhiteSpace(performedBy))
            throw new UnauthorizedAccessException("Authentication required");

        await _dynamo.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = $"{ExperimentPkPrefix}{experimentId}" },
                ["SK"] = new AttributeValue { S = $"{ProtocolSkPrefix}{protocolKey}" }
            },
            ConditionExpression = "attribute_exists(PK) AND attribute_exists(SK)"
        }, ct);
    }

    // ----------------- shared write path -----------------

    private enum WriteMode { Create, Update }

    private async Task<ProtocolSessionDto> WriteProtocolSessionAsync(
        string experimentId,
        string protocolKey,
        UpsertProtocolSessionRequest request,
        string performedBy,
        WriteMode mode,
        CancellationToken ct)
    {
        experimentId = (experimentId ?? string.Empty).Trim();
        protocolKey = NormaliseKey(protocolKey);
        performedBy = (performedBy ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(experimentId))
            throw new ArgumentException("Experiment ID is required");
        if (string.IsNullOrWhiteSpace(protocolKey))
            throw new ArgumentException("Protocol key is required");
        if (request == null)
            throw new ArgumentException("Request payload is required");
        if (string.IsNullOrWhiteSpace(performedBy))
            throw new UnauthorizedAccessException("Authentication required");

        NormaliseAndValidateRequest(request);

        // Guard: experiment exists + sessionTypeKey exists in experiment.data.sessionTypes
        var exp = await LoadExperimentOrThrowAsync(experimentId, ct);
        EnsureSessionTypeExists(exp, request.SessionTypeKey);

        var nowIso = DateTime.UtcNow.ToString("O");

        var pk = $"{ExperimentPkPrefix}{experimentId}";
        var sk = $"{ProtocolSkPrefix}{protocolKey}";

        var dataAttr = BuildProtocolDataAttribute(protocolKey, request);

        var condition = mode == WriteMode.Create
            ? "attribute_not_exists(PK) AND attribute_not_exists(SK)"
            : "attribute_exists(PK) AND attribute_exists(SK)";

        // Using UpdateItem for both create/update so we can keep if_not_exists for audit fields.
        await _dynamo.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = pk },
                ["SK"] = new AttributeValue { S = sk }
            },
            ConditionExpression = condition,

            UpdateExpression = @"
SET #type = if_not_exists(#type, :type),
    #data = :data,
    updatedBy = :u,
    updatedAt = :t,
    createdBy = if_not_exists(createdBy, :u),
    createdAt = if_not_exists(createdAt, :t)",

            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#type"] = "type",
                ["#data"] = "data"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":type"] = new AttributeValue { S = "ProtocolSession" },
                [":data"] = dataAttr,
                [":u"] = new AttributeValue { S = performedBy },
                [":t"] = new AttributeValue { S = nowIso }
            }
        }, ct);

        // Read-after-write: ensures DTO matches stored representation + audit fields
        return await GetProtocolSessionOrThrowAsync(experimentId, protocolKey, ct);
    }

    // ----------------- helpers -----------------

    private static string NormaliseKey(string? key)
        => (key ?? string.Empty).Trim().ToUpperInvariant();

    private static void NormaliseAndValidateRequest(UpsertProtocolSessionRequest request)
    {
        request.SessionTypeKey = NormaliseKey(request.SessionTypeKey);
        request.CadenceType = NormaliseKey(string.IsNullOrWhiteSpace(request.CadenceType) ? "ONCE" : request.CadenceType);

        if (string.IsNullOrWhiteSpace(request.SessionTypeKey))
            throw new ArgumentException("SessionTypeKey is required");

        ValidateCadence(request.CadenceType);

        if (request.MaxPerDay.HasValue && request.MaxPerDay.Value <= 0)
            throw new ArgumentException("MaxPerDay must be > 0 when provided");

        // Optional strictness:
        // - windowStartLocal/windowEndLocal: TimeOnly.ParseExact("HH:mm")
        // - weekday: 1..7 or 0..6 depending on your convention
    }

    private static void ValidateCadence(string cadenceType)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ONCE", "DAILY", "WEEKLY", "ADHOC"
        };

        if (!allowed.Contains(cadenceType))
            throw new ArgumentException($"CadenceType must be one of: {string.Join(", ", allowed)}");
    }

    private static AttributeValue BuildProtocolDataAttribute(string protocolKey, UpsertProtocolSessionRequest request)
    {
        var data = new Dictionary<string, object?>
        {
            ["protocolKey"] = protocolKey,
            ["sessionTypeKey"] = request.SessionTypeKey,
            ["order"] = request.Order,
            ["cadenceType"] = request.CadenceType,
            ["maxPerDay"] = request.MaxPerDay,
            ["windowStartLocal"] = request.WindowStartLocal,
            ["windowEndLocal"] = request.WindowEndLocal,
            ["weekday"] = request.Weekday
        };

        return new AttributeValue
        {
            M = DynamoDBHelper.JsonToAttributeValue(JsonSerializer.SerializeToElement(data))
        };
    }

    private async Task<ProtocolSessionDto> GetProtocolSessionOrThrowAsync(
        string experimentId,
        string protocolKey,
        CancellationToken ct)
    {
        var dto = await GetProtocolSessionAsync(experimentId, protocolKey, ct);
        if (dto == null)
            throw new KeyNotFoundException($"Protocol session '{protocolKey}' not found for experiment '{experimentId}'");
        return dto;
    }

    private async Task<ExperimentDto> LoadExperimentOrThrowAsync(string experimentId, CancellationToken ct)
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
            throw new KeyNotFoundException($"Experiment '{experimentId}' not found");

        return ExperimentItemMapper.MapExperimentDtoFromItem(resp.Item, experimentId);
    }

    private static void EnsureSessionTypeExists(ExperimentDto exp, string sessionTypeKey)
    {
        if (exp.Data?.SessionTypes == null || exp.Data.SessionTypes.Count == 0)
            throw new ArgumentException("Experiment has no sessionTypes defined");

        var exists = exp.Data.SessionTypes.Keys.Any(k => string.Equals(k, sessionTypeKey, StringComparison.OrdinalIgnoreCase));
        if (!exists)
            throw new ArgumentException($"SessionTypeKey '{sessionTypeKey}' does not exist in experiment.sessionTypes");
    }

    private static ProtocolSessionDto MapToDto(
        string experimentId,
        string protocolKey,
        object? dataObj,
        Dictionary<string, AttributeValue> item)
    {
        var data = dataObj as Dictionary<string, object> ?? new Dictionary<string, object>();

        string GetString(string k)
            => data.TryGetValue(k, out var v) && v != null ? v.ToString() ?? string.Empty : string.Empty;

        int GetInt(string k)
            => data.TryGetValue(k, out var v) && v != null && int.TryParse(v.ToString(), out var i) ? i : 0;

        int? GetNullableInt(string k)
            => data.TryGetValue(k, out var v) && v != null && int.TryParse(v.ToString(), out var i) ? i : null;

        return new ProtocolSessionDto
        {
            ExperimentId = experimentId,
            ProtocolKey = protocolKey,
            SessionTypeKey = GetString("sessionTypeKey"),
            Order = GetInt("order"),
            CadenceType = string.IsNullOrWhiteSpace(GetString("cadenceType")) ? "ONCE" : GetString("cadenceType"),
            MaxPerDay = GetNullableInt("maxPerDay"),
            WindowStartLocal = data.TryGetValue("windowStartLocal", out var ws) ? ws?.ToString() : null,
            WindowEndLocal = data.TryGetValue("windowEndLocal", out var we) ? we?.ToString() : null,
            Weekday = GetNullableInt("weekday"),
            CreatedAt = item.GetValueOrDefault("createdAt")?.S,
            UpdatedAt = item.GetValueOrDefault("updatedAt")?.S
        };
    }
}