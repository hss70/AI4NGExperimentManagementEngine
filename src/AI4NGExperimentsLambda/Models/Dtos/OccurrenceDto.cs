namespace AI4NGExperimentsLambda.Models.Dtos;

public sealed class OccurrenceDto
{
    public string ExperimentId { get; init; } = string.Empty;
    public string ParticipantId { get; init; } = string.Empty;

    public string OccurrenceKey { get; init; } = string.Empty;
    public string? ProtocolSessionKey { get; init; }
    public string SessionTypeKey { get; init; } = string.Empty;

    public string OccurrenceType { get; init; } = string.Empty;
    public bool IsRequired { get; init; }

    public string Status { get; init; } = string.Empty;

    public string? ScheduledAt { get; init; }
    public string? AvailableFrom { get; init; }
    public string? AvailableTo { get; init; }

    public string? DateLocal { get; init; }
    public int? StudyDay { get; init; }
    public int? StudyWeek { get; init; }
    public string? Timezone { get; init; }

    public string? StartedAt { get; init; }
    public string? EndedAt { get; init; }
    public string? LastResumedAt { get; init; }

    public int CurrentTaskIndex { get; init; }
    public int CompletedTaskCount { get; init; }
    public int TotalTaskCount { get; init; }

    public List<string> TaskOrder { get; init; } = new();
    public List<OccurrenceTaskStateDto> TaskState { get; init; } = new();

    public OccurrenceClassifierPolicyDto? ClassifierPolicy { get; init; }
    public OccurrenceQuestionBankSliceDto? QuestionBankSlice { get; init; }

    public string? Source { get; init; }

    public string? CreatedAt { get; init; }
    public string? UpdatedAt { get; init; }
}

public sealed class OccurrenceTaskStateDto
{
    public int Order { get; init; }
    public string TaskKey { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? StartedAt { get; init; }
    public string? EndedAt { get; init; }
    public bool IsRequired { get; init; }
    public string? BlockingReason { get; init; }
}

public sealed class OccurrenceClassifierPolicyDto
{
    public bool RequiresValidClassifier { get; init; }
    public bool RetrainIfCapRemoved { get; init; }
    public bool AllowPreviousClassifierReuse { get; init; }
    public int? ClassifierFreshnessHours { get; init; }
    public string TrainingRequirement { get; init; } = string.Empty;
}

public sealed class OccurrenceQuestionBankSliceDto
{
    public string? BankKey { get; init; }
    public List<string> QuestionIds { get; init; } = new();
    public int TotalQuestionsAvailable { get; init; }
    public int QuestionsReleasedThisOccurrence { get; init; }
}