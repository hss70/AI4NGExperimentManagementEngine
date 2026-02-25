namespace AI4NGExperimentsLambda.Models;

public class SessionData
{
    public string Date { get; set; } = string.Empty;
    public string SessionType { get; set; } = string.Empty;
    public string SessionName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SequenceNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public SessionMetadata Metadata { get; set; } = new();
    // Optional: allow updating task order via session update
    public List<string>? TaskOrder { get; set; }
}
