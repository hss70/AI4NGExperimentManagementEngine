namespace AI4NGExperimentsLambda.Models;

public class SessionData
{
    public string SessionType { get; set; } = string.Empty;
    public string SessionName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SequenceNumber { get; set; }
    public string Status { get; set; } = string.Empty;
}
