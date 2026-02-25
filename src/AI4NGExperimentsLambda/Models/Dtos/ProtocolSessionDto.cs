namespace AI4NGExperimentsLambda.Models.Dtos;

public sealed class ProtocolSessionDto
{
    public string ExperimentId { get; init; } = string.Empty;
    public string ProtocolSessionKey { get; init; } = string.Empty; // FIRST/DAILY/WEEKLY

    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    // once|daily|weekly (string for now, can be enum later)
    public string Cadence { get; init; } = string.Empty;

    public List<string> TaskSequence { get; init; } = new();

    public int? EstimatedDuration
    {
        get; init;
    }

    // Optional “windowing” in local time (HH:mm)
    public string? WindowStartLocal
    {
        get; init;
    }
    public string? WindowEndLocal
    {
        get; init;
    }

    public string? UpdatedAt
    {
        get; init;
    }
}