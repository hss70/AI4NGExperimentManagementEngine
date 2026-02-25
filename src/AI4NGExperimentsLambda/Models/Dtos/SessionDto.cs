namespace AI4NGExperimentsLambda.Models.Dtos;

public class SessionDto
{
    public string? SessionId { get; set; }
    public string? ExperimentId { get; set; }
    public SessionData Data { get; set; } = new();
    public List<string> TaskOrder { get; set; } = new();
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
}
