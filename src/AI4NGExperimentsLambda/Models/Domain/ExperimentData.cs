namespace AI4NGExperimentsLambda.Models;

public class ExperimentData
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, SessionType> SessionTypes { get; set; } = new();
    public string? Status { get; set; }
}
