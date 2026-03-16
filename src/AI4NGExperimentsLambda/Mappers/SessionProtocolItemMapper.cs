using Amazon.DynamoDBv2.Model;
using AI4NGExperimentsLambda.Models;
using AI4NGExperimentsLambda.Models.Dtos;

namespace AI4NGExperimentsLambda.Mappers;

public static class ProtocolSessionItemMapper
{
    public static ProtocolSessionDto MapProtocolSessionDto(
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
            CadenceType = string.IsNullOrWhiteSpace(GetString("cadenceType"))
                ? "ONCE"
                : GetString("cadenceType"),
            MaxPerDay = GetNullableInt("maxPerDay"),
            WindowStartLocal = data.TryGetValue("windowStartLocal", out var ws) ? ws?.ToString() : null,
            WindowEndLocal = data.TryGetValue("windowEndLocal", out var we) ? we?.ToString() : null,
            Weekday = GetNullableInt("weekday"),
            CreatedAt = item.GetValueOrDefault("createdAt")?.S,
            UpdatedAt = item.GetValueOrDefault("updatedAt")?.S
        };
    }

    public static SessionDto MapProtocolSessionToSessionDto(
        string experimentId,
        ProtocolSessionDto protocol,
        ExperimentDto experiment)
    {
        var sessionType = ResolveSessionType(experiment, protocol.SessionTypeKey);

        return new SessionDto
        {
            SessionId = protocol.ProtocolKey,
            ExperimentId = experimentId,
            CreatedAt = protocol.CreatedAt,
            UpdatedAt = protocol.UpdatedAt,
            TaskOrder = sessionType?.Tasks?.ToList() ?? new List<string>(),
            Data = new SessionData
            {
                SessionType = protocol.SessionTypeKey ?? string.Empty,
                SessionName = sessionType?.Name ?? protocol.SessionTypeKey ?? string.Empty,
                Description = sessionType?.Description ?? string.Empty,
                SequenceNumber = protocol.Order,
                Status = "AVAILABLE",
            }
        };
    }

    private static SessionType? ResolveSessionType(
        ExperimentDto experiment,
        string? sessionTypeKey)
    {
        if (string.IsNullOrWhiteSpace(sessionTypeKey))
            return null;

        if (experiment.Data?.SessionTypes == null)
            return null;

        foreach (var kvp in experiment.Data.SessionTypes)
        {
            if (string.Equals(kvp.Key, sessionTypeKey, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;
        }

        return null;
    }
}