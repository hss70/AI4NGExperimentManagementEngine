namespace AI4NGExperimentsLambda.Models;

public class Session
{
    public string SessionId { get; set; } = string.Empty;
    public string ExperimentId { get; set; } = string.Empty;
    public SessionData Data { get; set; } = new();
    public List<string> TaskOrder { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
