namespace AI4NGExperimentsLambda.Models.Dtos;

public sealed class OccurrenceDto
{
    public string ExperimentId { get; init; } = string.Empty;
    public string ParticipantId { get; init; } = string.Empty;

    public string OccurrenceKey { get; init; } = string.Empty;      // DAILY#2026-03-05 etc.
    public string ProtocolSessionKey { get; init; } = string.Empty; // DAILY/FIRST/WEEKLY

    public string Status { get; init; } = "scheduled";

    public string? ScheduledAt
    {
        get; init;
    }
    public string? StartedAt
    {
        get; init;
    }
    public string? EndedAt
    {
        get; init;
    }

    public List<OccurrenceTaskStateDto> TaskState { get; init; } = new();

    public sealed class OccurrenceTaskStateDto
    {
        public int Order
        {
            get; init;
        }
        public string TaskKey { get; init; } = string.Empty;
        public string Status { get; init; } = "pending"; // pending/done/skipped
    }
}