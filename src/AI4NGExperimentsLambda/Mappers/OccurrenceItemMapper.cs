using Amazon.DynamoDBv2.Model;
using AI4NGExperimentsLambda.Models;
using AI4NGExperimentsLambda.Models.Dtos;

namespace AI4NGExperimentsLambda.Mappers;

public static class OccurrenceItemMapper
{
    private const string OccurrencePkPrefix = "OCCURRENCE#";

    public static string BuildPk(string experimentId, string participantId)
        => $"{OccurrencePkPrefix}{experimentId}#{participantId}";

    public static string BuildSk(string occurrenceKey)
        => occurrenceKey;

    public static Dictionary<string, AttributeValue> MapToItem(
        ParticipantSessionOccurrence occurrence,
        string createdBy,
        string updatedBy)
    {
        var createdAt = string.IsNullOrWhiteSpace(occurrence.CreatedAt)
            ? DateTime.UtcNow.ToString("O")
            : occurrence.CreatedAt;

        var updatedAt = string.IsNullOrWhiteSpace(occurrence.UpdatedAt)
            ? DateTime.UtcNow.ToString("O")
            : occurrence.UpdatedAt;

        return new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = BuildPk(occurrence.ExperimentId, occurrence.ParticipantId) },
            ["SK"] = new AttributeValue { S = BuildSk(occurrence.OccurrenceKey) },
            ["type"] = new AttributeValue { S = "ParticipantSessionOccurrence" },
            ["data"] = new AttributeValue { M = MapOccurrenceDataToAttributeMap(occurrence) },
            ["createdAt"] = new AttributeValue { S = createdAt },
            ["createdBy"] = new AttributeValue { S = string.IsNullOrWhiteSpace(occurrence.CreatedBy) ? createdBy : occurrence.CreatedBy },
            ["updatedAt"] = new AttributeValue { S = updatedAt },
            ["updatedBy"] = new AttributeValue { S = string.IsNullOrWhiteSpace(occurrence.UpdatedBy) ? updatedBy : occurrence.UpdatedBy }
        };
    }

    public static ParticipantSessionOccurrence MapOccurrenceFromItem(Dictionary<string, AttributeValue> item)
    {
        var occurrence = new ParticipantSessionOccurrence();

        if (item.TryGetValue("data", out var dataAttr) && dataAttr.M != null)
        {
            occurrence = MapOccurrenceFromMap(dataAttr.M);
        }

        occurrence.CreatedAt = item.GetValueOrDefault("createdAt")?.S ?? occurrence.CreatedAt;
        occurrence.CreatedBy = item.GetValueOrDefault("createdBy")?.S ?? occurrence.CreatedBy;
        occurrence.UpdatedAt = item.GetValueOrDefault("updatedAt")?.S ?? occurrence.UpdatedAt;
        occurrence.UpdatedBy = item.GetValueOrDefault("updatedBy")?.S ?? occurrence.UpdatedBy;

        return occurrence;
    }

    public static OccurrenceDto MapToDto(ParticipantSessionOccurrence occurrence)
    {
        return new OccurrenceDto
        {
            ExperimentId = occurrence.ExperimentId,
            ParticipantId = occurrence.ParticipantId,
            OccurrenceKey = occurrence.OccurrenceKey,
            ProtocolSessionKey = occurrence.ProtocolSessionKey,
            SessionTypeKey = occurrence.SessionTypeKey,
            OccurrenceType = occurrence.OccurrenceType,
            IsRequired = occurrence.IsRequired,
            Status = occurrence.Status,
            ScheduledAt = occurrence.ScheduledAt,
            AvailableFrom = occurrence.AvailableFrom,
            AvailableTo = occurrence.AvailableTo,
            DateLocal = occurrence.DateLocal,
            StudyDay = occurrence.StudyDay,
            StudyWeek = occurrence.StudyWeek,
            Timezone = occurrence.Timezone,
            StartedAt = occurrence.StartedAt,
            EndedAt = occurrence.EndedAt,
            LastResumedAt = occurrence.LastResumedAt,
            CurrentTaskIndex = occurrence.CurrentTaskIndex,
            CompletedTaskCount = occurrence.CompletedTaskCount,
            TotalTaskCount = occurrence.TotalTaskCount,
            TaskOrder = occurrence.TaskOrder?.ToList() ?? new List<string>(),
            TaskState = occurrence.TaskState?
                .OrderBy(x => x.Order)
                .Select(MapTaskStateToDto)
                .ToList() ?? new List<OccurrenceTaskStateDto>(),
            ClassifierPolicy = occurrence.ClassifierPolicy == null
                ? null
                : MapClassifierPolicyToDto(occurrence.ClassifierPolicy),
            QuestionBankSlice = occurrence.QuestionBankSlice == null
                ? null
                : MapQuestionBankSliceToDto(occurrence.QuestionBankSlice),
            Source = occurrence.Source,
            CreatedAt = occurrence.CreatedAt,
            UpdatedAt = occurrence.UpdatedAt
        };
    }

    public static ParticipantSessionOccurrence MapOccurrenceFromMap(Dictionary<string, AttributeValue> map)
    {
        var occurrence = new ParticipantSessionOccurrence
        {
            ExperimentId = map.GetValueOrDefault("ExperimentId")?.S ?? string.Empty,
            ParticipantId = map.GetValueOrDefault("ParticipantId")?.S ?? string.Empty,
            OccurrenceKey = map.GetValueOrDefault("OccurrenceKey")?.S ?? string.Empty,
            ProtocolSessionKey = NormalizeOptionalString(map.GetValueOrDefault("ProtocolSessionKey")?.S),
            SessionTypeKey = map.GetValueOrDefault("SessionTypeKey")?.S ?? string.Empty,
            OccurrenceType = map.GetValueOrDefault("OccurrenceType")?.S ?? string.Empty,
            IsRequired = TryGetBool(map, "IsRequired") ?? false,
            Status = map.GetValueOrDefault("Status")?.S ?? string.Empty,
            ScheduledAt = NormalizeOptionalString(map.GetValueOrDefault("ScheduledAt")?.S),
            AvailableFrom = NormalizeOptionalString(map.GetValueOrDefault("AvailableFrom")?.S),
            AvailableTo = NormalizeOptionalString(map.GetValueOrDefault("AvailableTo")?.S),
            DateLocal = NormalizeOptionalString(map.GetValueOrDefault("DateLocal")?.S),
            StudyDay = TryGetNullableInt(map, "StudyDay"),
            StudyWeek = TryGetNullableInt(map, "StudyWeek"),
            Timezone = NormalizeOptionalString(map.GetValueOrDefault("Timezone")?.S),
            StartedAt = NormalizeOptionalString(map.GetValueOrDefault("StartedAt")?.S),
            EndedAt = NormalizeOptionalString(map.GetValueOrDefault("EndedAt")?.S),
            LastResumedAt = NormalizeOptionalString(map.GetValueOrDefault("LastResumedAt")?.S),
            CurrentTaskIndex = TryGetNullableInt(map, "CurrentTaskIndex") ?? 0,
            CompletedTaskCount = TryGetNullableInt(map, "CompletedTaskCount") ?? 0,
            TotalTaskCount = TryGetNullableInt(map, "TotalTaskCount") ?? 0,
            TaskOrder = TryGetStringList(map, "TaskOrder"),
            TaskState = TryGetTaskStateList(map, "TaskState"),
            Source = NormalizeOptionalString(map.GetValueOrDefault("Source")?.S),
            Notes = NormalizeOptionalString(map.GetValueOrDefault("Notes")?.S)
        };

        if (map.TryGetValue("ClassifierPolicy", out var classifierAttr) && classifierAttr.M != null)
        {
            occurrence.ClassifierPolicy = MapClassifierPolicyFromMap(classifierAttr.M);
        }

        if (map.TryGetValue("QuestionBankSlice", out var questionBankAttr) && questionBankAttr.M != null)
        {
            occurrence.QuestionBankSlice = MapQuestionBankSliceFromMap(questionBankAttr.M);
        }

        return occurrence;
    }

    public static Dictionary<string, AttributeValue> MapOccurrenceDataToAttributeMap(ParticipantSessionOccurrence occurrence)
    {
        var map = new Dictionary<string, AttributeValue>
        {
            ["ExperimentId"] = new AttributeValue { S = occurrence.ExperimentId },
            ["ParticipantId"] = new AttributeValue { S = occurrence.ParticipantId },
            ["OccurrenceKey"] = new AttributeValue { S = occurrence.OccurrenceKey },
            ["SessionTypeKey"] = new AttributeValue { S = occurrence.SessionTypeKey },
            ["OccurrenceType"] = new AttributeValue { S = occurrence.OccurrenceType },
            ["IsRequired"] = new AttributeValue { BOOL = occurrence.IsRequired },
            ["Status"] = new AttributeValue { S = occurrence.Status },
            ["CurrentTaskIndex"] = new AttributeValue { N = occurrence.CurrentTaskIndex.ToString() },
            ["CompletedTaskCount"] = new AttributeValue { N = occurrence.CompletedTaskCount.ToString() },
            ["TotalTaskCount"] = new AttributeValue { N = occurrence.TotalTaskCount.ToString() },
            ["TaskOrder"] = new AttributeValue
            {
                L = (occurrence.TaskOrder ?? new List<string>())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => new AttributeValue { S = x.Trim() })
                    .ToList()
            },
            ["TaskState"] = new AttributeValue
            {
                L = (occurrence.TaskState ?? new List<OccurrenceTaskState>())
                    .OrderBy(x => x.Order)
                    .Select(x => new AttributeValue { M = MapTaskStateToAttributeMap(x) })
                    .ToList()
            }
        };

        AddOptionalString(map, "ProtocolSessionKey", occurrence.ProtocolSessionKey);
        AddOptionalString(map, "ScheduledAt", occurrence.ScheduledAt);
        AddOptionalString(map, "AvailableFrom", occurrence.AvailableFrom);
        AddOptionalString(map, "AvailableTo", occurrence.AvailableTo);
        AddOptionalString(map, "DateLocal", occurrence.DateLocal);
        AddOptionalString(map, "Timezone", occurrence.Timezone);
        AddOptionalString(map, "StartedAt", occurrence.StartedAt);
        AddOptionalString(map, "EndedAt", occurrence.EndedAt);
        AddOptionalString(map, "LastResumedAt", occurrence.LastResumedAt);
        AddOptionalString(map, "Source", occurrence.Source);
        AddOptionalString(map, "Notes", occurrence.Notes);

        AddOptionalInt(map, "StudyDay", occurrence.StudyDay);
        AddOptionalInt(map, "StudyWeek", occurrence.StudyWeek);

        if (occurrence.ClassifierPolicy != null)
        {
            map["ClassifierPolicy"] = new AttributeValue
            {
                M = MapClassifierPolicyToAttributeMap(occurrence.ClassifierPolicy)
            };
        }

        if (occurrence.QuestionBankSlice != null)
        {
            map["QuestionBankSlice"] = new AttributeValue
            {
                M = MapQuestionBankSliceToAttributeMap(occurrence.QuestionBankSlice)
            };
        }

        return map;
    }

    private static Dictionary<string, AttributeValue> MapTaskStateToAttributeMap(OccurrenceTaskState state)
    {
        var map = new Dictionary<string, AttributeValue>
        {
            ["Order"] = new AttributeValue { N = state.Order.ToString() },
            ["TaskKey"] = new AttributeValue { S = state.TaskKey },
            ["Status"] = new AttributeValue { S = state.Status },
            ["IsRequired"] = new AttributeValue { BOOL = state.IsRequired }
        };

        AddOptionalString(map, "StartedAt", state.StartedAt);
        AddOptionalString(map, "EndedAt", state.EndedAt);
        AddOptionalString(map, "BlockingReason", state.BlockingReason);

        return map;
    }

    private static OccurrenceTaskState MapTaskStateFromMap(Dictionary<string, AttributeValue> map)
    {
        return new OccurrenceTaskState
        {
            Order = TryGetNullableInt(map, "Order") ?? 0,
            TaskKey = map.GetValueOrDefault("TaskKey")?.S ?? string.Empty,
            Status = map.GetValueOrDefault("Status")?.S ?? string.Empty,
            StartedAt = NormalizeOptionalString(map.GetValueOrDefault("StartedAt")?.S),
            EndedAt = NormalizeOptionalString(map.GetValueOrDefault("EndedAt")?.S),
            IsRequired = TryGetBool(map, "IsRequired") ?? true,
            BlockingReason = NormalizeOptionalString(map.GetValueOrDefault("BlockingReason")?.S)
        };
    }

    private static OccurrenceTaskStateDto MapTaskStateToDto(OccurrenceTaskState state)
    {
        return new OccurrenceTaskStateDto
        {
            Order = state.Order,
            TaskKey = state.TaskKey,
            Status = state.Status,
            StartedAt = state.StartedAt,
            EndedAt = state.EndedAt,
            IsRequired = state.IsRequired,
            BlockingReason = state.BlockingReason
        };
    }

    private static Dictionary<string, AttributeValue> MapClassifierPolicyToAttributeMap(OccurrenceClassifierPolicy policy)
    {
        var map = new Dictionary<string, AttributeValue>
        {
            ["RequiresValidClassifier"] = new AttributeValue { BOOL = policy.RequiresValidClassifier },
            ["RetrainIfCapRemoved"] = new AttributeValue { BOOL = policy.RetrainIfCapRemoved },
            ["AllowPreviousClassifierReuse"] = new AttributeValue { BOOL = policy.AllowPreviousClassifierReuse },
            ["TrainingRequirement"] = new AttributeValue { S = policy.TrainingRequirement }
        };

        AddOptionalInt(map, "ClassifierFreshnessHours", policy.ClassifierFreshnessHours);

        return map;
    }

    private static OccurrenceClassifierPolicy MapClassifierPolicyFromMap(Dictionary<string, AttributeValue> map)
    {
        return new OccurrenceClassifierPolicy
        {
            RequiresValidClassifier = TryGetBool(map, "RequiresValidClassifier") ?? false,
            RetrainIfCapRemoved = TryGetBool(map, "RetrainIfCapRemoved") ?? false,
            AllowPreviousClassifierReuse = TryGetBool(map, "AllowPreviousClassifierReuse") ?? false,
            ClassifierFreshnessHours = TryGetNullableInt(map, "ClassifierFreshnessHours"),
            TrainingRequirement = map.GetValueOrDefault("TrainingRequirement")?.S ?? string.Empty
        };
    }

    private static OccurrenceClassifierPolicyDto MapClassifierPolicyToDto(OccurrenceClassifierPolicy policy)
    {
        return new OccurrenceClassifierPolicyDto
        {
            RequiresValidClassifier = policy.RequiresValidClassifier,
            RetrainIfCapRemoved = policy.RetrainIfCapRemoved,
            AllowPreviousClassifierReuse = policy.AllowPreviousClassifierReuse,
            ClassifierFreshnessHours = policy.ClassifierFreshnessHours,
            TrainingRequirement = policy.TrainingRequirement
        };
    }

    private static Dictionary<string, AttributeValue> MapQuestionBankSliceToAttributeMap(OccurrenceQuestionBankSlice slice)
    {
        var map = new Dictionary<string, AttributeValue>
        {
            ["QuestionIds"] = new AttributeValue
            {
                L = (slice.QuestionIds ?? new List<string>())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => new AttributeValue { S = x.Trim() })
                    .ToList()
            },
            ["TotalQuestionsAvailable"] = new AttributeValue { N = slice.TotalQuestionsAvailable.ToString() },
            ["QuestionsReleasedThisOccurrence"] = new AttributeValue { N = slice.QuestionsReleasedThisOccurrence.ToString() }
        };

        AddOptionalString(map, "BankKey", slice.BankKey);

        return map;
    }

    private static OccurrenceQuestionBankSlice MapQuestionBankSliceFromMap(Dictionary<string, AttributeValue> map)
    {
        return new OccurrenceQuestionBankSlice
        {
            BankKey = NormalizeOptionalString(map.GetValueOrDefault("BankKey")?.S),
            QuestionIds = TryGetStringList(map, "QuestionIds"),
            TotalQuestionsAvailable = TryGetNullableInt(map, "TotalQuestionsAvailable") ?? 0,
            QuestionsReleasedThisOccurrence = TryGetNullableInt(map, "QuestionsReleasedThisOccurrence") ?? 0
        };
    }

    private static OccurrenceQuestionBankSliceDto MapQuestionBankSliceToDto(OccurrenceQuestionBankSlice slice)
    {
        return new OccurrenceQuestionBankSliceDto
        {
            BankKey = slice.BankKey,
            QuestionIds = slice.QuestionIds?.ToList() ?? new List<string>(),
            TotalQuestionsAvailable = slice.TotalQuestionsAvailable,
            QuestionsReleasedThisOccurrence = slice.QuestionsReleasedThisOccurrence
        };
    }

    private static List<OccurrenceTaskState> TryGetTaskStateList(Dictionary<string, AttributeValue> map, string key)
    {
        if (!map.TryGetValue(key, out var attr) || attr.L == null)
            return new List<OccurrenceTaskState>();

        return attr.L
            .Where(x => x.M != null)
            .Select(x => MapTaskStateFromMap(x.M))
            .OrderBy(x => x.Order)
            .ToList();
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

    private static int? TryGetNullableInt(Dictionary<string, AttributeValue> map, string key)
    {
        if (!map.TryGetValue(key, out var attr))
            return null;

        if (string.IsNullOrWhiteSpace(attr.N))
            return null;

        return int.TryParse(attr.N, out var value) ? value : null;
    }

    private static bool? TryGetBool(Dictionary<string, AttributeValue> map, string key)
    {
        if (!map.TryGetValue(key, out var attr))
            return null;

        return attr.BOOL;
    }

    private static void AddOptionalString(Dictionary<string, AttributeValue> map, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            map[key] = new AttributeValue { S = value.Trim() };
        }
    }

    private static void AddOptionalInt(Dictionary<string, AttributeValue> map, string key, int? value)
    {
        if (value.HasValue)
        {
            map[key] = new AttributeValue { N = value.Value.ToString() };
        }
    }

    private static string? NormalizeOptionalString(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

public static class OccurrenceKeyHelper
{
    public static string BuildDailyKey(DateOnly localDate)
        => $"DAILY#{localDate:yyyy-MM-dd}";

    public static string BuildWeeklyKey(int year, int week)
        => $"WEEKLY#{year}-W{week:D2}";

    public static string BuildFirstKey()
        => "FIRST";

    public static string BuildOptionalKey(string prefix)
        => $"{prefix.ToUpperInvariant()}#{DateTime.UtcNow:O}#{Guid.NewGuid():N[..6]}";
}