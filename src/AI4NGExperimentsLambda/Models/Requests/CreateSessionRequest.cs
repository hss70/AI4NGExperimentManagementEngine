namespace AI4NGExperimentsLambda.Models;

public class CreateSessionRequest
{
    public string ExperimentId { get; set; } = string.Empty;
    public string SessionType { get; set; } = string.Empty;
    public string SessionName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
}
