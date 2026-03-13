using AI4NGExperimentsLambda.Models;
using AI4NGExperimentsLambda.Models.Dtos;
using Amazon.DynamoDBv2.Model;
namespace AI4NGExperimentsLambda.Helpers;

public static class ExperimentItemMapper
{
    public static ExperimentDto MapExperimentDtoFromItem(
    Dictionary<string, AttributeValue> item,
    string experimentId)
    {
        return new ExperimentDto
        {
            Id = experimentId,
            Status = item.GetValueOrDefault("status")?.S ?? string.Empty,
            Data = MapExperimentDataFromItem(item),
            UpdatedAt = item.GetValueOrDefault("updatedAt")?.S,
            UpdatedBy = item.GetValueOrDefault("updatedBy")?.S,
            CreatedAt = item.GetValueOrDefault("createdAt")?.S,
            CreatedBy = item.GetValueOrDefault("createdBy")?.S ?? string.Empty
        };
    }
    public static ExperimentData MapExperimentDataFromItem(Dictionary<string, AttributeValue> item)
    {
        if (!item.TryGetValue("data", out var dataAttr) || dataAttr.M == null)
            return new ExperimentData();

        return MapExperimentDataFromMap(dataAttr.M);
    }

    private static ExperimentData MapExperimentDataFromMap(Dictionary<string, AttributeValue> map)
    {
        var data = new ExperimentData
        {
            Name = map.GetValueOrDefault("Name")?.S ?? string.Empty,
            Description = map.GetValueOrDefault("Description")?.S ?? string.Empty,
            StudyStartDate = map.GetValueOrDefault("StudyStartDate")?.S,
            StudyEndDate = map.GetValueOrDefault("StudyEndDate")?.S,
            ParticipantDurationDays = TryGetNullableInt(map, "ParticipantDurationDays"),
            SessionTypes = new Dictionary<string, SessionType>(StringComparer.OrdinalIgnoreCase)
        };

        if (map.TryGetValue("SessionTypes", out var sessionTypesAttr) && sessionTypesAttr.M != null)
        {
            foreach (var kvp in sessionTypesAttr.M)
            {
                var sessionKey = (kvp.Key ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(sessionKey))
                    continue;

                var sessionMap = kvp.Value?.M;
                if (sessionMap == null)
                    continue;

                data.SessionTypes[sessionKey] = new SessionType
                {
                    Name = sessionMap.GetValueOrDefault("Name")?.S ?? string.Empty,
                    Description = sessionMap.GetValueOrDefault("Description")?.S ?? string.Empty,
                    EstimatedDurationMinutes = TryGetNullableInt(sessionMap, "EstimatedDurationMinutes") ?? 0,
                    Schedule = sessionMap.GetValueOrDefault("Schedule")?.S,
                    Tasks = TryGetStringList(sessionMap, "Tasks")
                };
            }
        }

        return data;
    }

    private static int? TryGetNullableInt(Dictionary<string, AttributeValue> map, string key)
    {
        if (!map.TryGetValue(key, out var attr))
            return null;

        if (string.IsNullOrWhiteSpace(attr.N))
            return null;

        return int.TryParse(attr.N, out var value) ? value : null;
    }

    private static List<string> TryGetStringList(Dictionary<string, AttributeValue> map, string key)
    {
        if (!map.TryGetValue(key, out var attr) || attr.L == null)
            return new List<string>();

        return attr.L
            .Where(x => !string.IsNullOrWhiteSpace(x.S))
            .Select(x => x.S!.Trim())
            .ToList();
    }
}