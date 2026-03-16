using AI4NGExperimentsLambda.Models.Constants;

namespace AI4NGExperimentsLambda.Models.Requests.Participant;

public sealed class StartOccurrenceRequest
{
    public bool ResumeIfInProgress { get; init; } = true;

    // use_previous / retrain / auto
    public string? ClassifierAction { get; init; }

    public string? Timezone { get; init; }
}
public sealed class CompleteOccurrenceRequest
{
    public bool MarkIncompleteTasksAsSkipped { get; init; }
    public string? Notes { get; init; }
}
public sealed class CreateOccurrenceRequest
{
    public string SessionTypeKey { get; init; } = string.Empty;
    public string OccurrenceType { get; init; } = OccurrenceTypes.Optional;

    public string? ProtocolSessionKey { get; init; }
    public bool IsRequired { get; init; }

    public string? ScheduledAt { get; init; }
    public string? Timezone { get; init; }

    public string? Source { get; init; }
}

public sealed class RescheduleOccurrenceRequest
{
    public string NewScheduledAt { get; init; } = string.Empty;
    public string? Reason { get; init; }
}

public sealed class UpdateOccurrenceTaskStateRequest
{
    public string TaskKey { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? BlockingReason { get; init; }
}