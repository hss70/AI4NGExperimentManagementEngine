namespace AI4NGExperimentsLambda.Models;

public class Experiment
{
    public string Id { get; set; } = string.Empty;
    public ExperimentData Data { get; set; } = new();
    public QuestionnaireConfig QuestionnaireConfig { get; set; } = new();
    public List<Session> Sessions { get; set; } = new();
    // Optional: seed sessions to be created during experiment creation
    public List<InitialSessionRequest>? InitialSessions { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
