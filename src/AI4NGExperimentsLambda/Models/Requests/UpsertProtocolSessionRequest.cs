namespace AI4NGExperimentsLambda.Models.Requests;

public sealed class UpsertProtocolSessionRequest
{
    public string ProtocolSessionKey { get; init; } = string.Empty; // FIRST/DAILY/WEEKLY
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Cadence { get; init; } = string.Empty; // once|daily|weekly
    public List<string> TaskSequence { get; init; } = new();
    public int? EstimatedDuration
    {
        get; init;
    }
    public string? WindowStartLocal
    {
        get; init;
    }
    public string? WindowEndLocal
    {
        get; init;
    }
}