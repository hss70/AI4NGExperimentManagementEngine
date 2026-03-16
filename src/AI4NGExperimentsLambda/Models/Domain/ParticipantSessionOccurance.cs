using AI4NGExperimentsLambda.Models.Constants;

namespace AI4NGExperimentsLambda.Models;

public sealed class ParticipantSessionOccurrence
{
    public string ExperimentId { get; set; } = string.Empty;
    public string ParticipantId { get; set; } = string.Empty;

    public string OccurrenceKey { get; set; } = string.Empty;
    public string? ProtocolSessionKey { get; set; }
    public string SessionTypeKey { get; set; } = string.Empty;

    // Protocol / optional / question bank / freeplay etc.
    public string OccurrenceType { get; set; } = OccurrenceTypes.Protocol;
    public bool IsRequired { get; set; } = true;

    // scheduled / in_progress / completed / skipped / cancelled
    public string Status { get; set; } = OccurrenceStatuses.Scheduled;

    public string? ScheduledAt { get; set; }
    public string? AvailableFrom { get; set; }
    public string? AvailableTo { get; set; }

    public string? DateLocal { get; set; }
    public int? StudyDay { get; set; }
    public int? StudyWeek { get; set; }
    public string? Timezone { get; set; }

    public string? StartedAt { get; set; }
    public string? EndedAt { get; set; }
    public string? LastResumedAt { get; set; }

    public int CurrentTaskIndex { get; set; } = 0;
    public int CompletedTaskCount { get; set; } = 0;
    public int TotalTaskCount { get; set; } = 0;

    public List<string> TaskOrder { get; set; } = new();
    public List<OccurrenceTaskState> TaskState { get; set; } = new();

    public OccurrenceClassifierPolicy? ClassifierPolicy { get; set; }
    public OccurrenceQuestionBankSlice? QuestionBankSlice { get; set; }

    public string? Source { get; set; } // Scheduled / ParticipantInitiated / ResearcherInitiated
    public string? Notes { get; set; }

    public string CreatedAt { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;
    public string UpdatedBy { get; set; } = string.Empty;
}

public sealed class OccurrenceTaskState
{
    public int Order { get; set; }
    public string TaskKey { get; set; } = string.Empty;

    // pending / in_progress / completed / skipped / blocked
    public string Status { get; set; } = OccurrenceTaskStatuses.Pending;

    public string? StartedAt { get; set; }
    public string? EndedAt { get; set; }

    public bool IsRequired { get; set; } = true;
    public string? BlockingReason { get; set; }
}

public sealed class OccurrenceClassifierPolicy
{
    public bool RequiresValidClassifier { get; set; }
    public bool RetrainIfCapRemoved { get; set; }
    public bool AllowPreviousClassifierReuse { get; set; }
    public int? ClassifierFreshnessHours { get; set; }

    // required / optional / not_required
    public string TrainingRequirement { get; set; } = ClassifierTrainingRequirements.NotRequired;
}

public sealed class OccurrenceQuestionBankSlice
{
    public string? BankKey { get; set; }
    public List<string> QuestionIds { get; set; } = new();
    public int TotalQuestionsAvailable { get; set; }
    public int QuestionsReleasedThisOccurrence { get; set; }
}