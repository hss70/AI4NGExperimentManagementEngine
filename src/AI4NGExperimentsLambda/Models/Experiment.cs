namespace AI4NGExperimentsLambda.Models;

public class Experiment
{
    public string Id { get; set; } = string.Empty;
    public ExperimentData Data { get; set; } = new();
    public QuestionnaireConfig QuestionnaireConfig { get; set; } = new();
    public List<Session> Sessions { get; set; } = new();
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ExperimentData
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class QuestionnaireConfig
{
    public List<string> QuestionnaireIds { get; set; } = new();
}

public class Session
{
    public string SessionId { get; set; } = string.Empty;
    public string ParticipantId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class SyncRequest
{
    public List<Session> Sessions { get; set; } = new();
}

public class MemberRequest
{
    public string Role { get; set; } = string.Empty;
}