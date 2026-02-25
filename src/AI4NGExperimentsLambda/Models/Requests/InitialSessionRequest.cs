namespace AI4NGExperimentsLambda.Models;

// For creating sessions as part of experiment creation
public class InitialSessionRequest
{
    public string SessionType { get; set; } = string.Empty;
    // If not provided, service may default to date-based sessionId (e.g., yyyy-MM-dd)
    public string? SessionId { get; set; }
    public string Date { get; set; } = string.Empty;
    public List<string>? TaskOrder { get; set; }
}
