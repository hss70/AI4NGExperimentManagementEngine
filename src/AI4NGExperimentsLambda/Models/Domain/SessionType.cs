namespace AI4NGExperimentsLambda.Models;

public class SessionType
{
    public string Name { get; set; } = string.Empty;
    public List<string> Questionnaires { get; set; } = new();
    public List<string> Tasks { get; set; } = new();
    public int EstimatedDuration { get; set; }
    public string? Schedule { get; set; }
}
