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
    public Dictionary<string, SessionType> SessionTypes { get; set; } = new();
    public string? Status { get; set; }
}

public class SessionType
{
    public string Name { get; set; } = string.Empty;
    public List<string> Questionnaires { get; set; } = new();
    public List<string> Tasks { get; set; } = new();
    public int EstimatedDuration { get; set; }
    public string? Schedule { get; set; }
}

public class QuestionnaireConfig
{
    public Dictionary<string, string> Schedule { get; set; } = new();
}

public class Session
{
    public string SessionId { get; set; } = string.Empty;
    public string ExperimentId { get; set; } = string.Empty;
    public SessionData Data { get; set; } = new();
    public List<string> TaskOrder { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

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
}

public class SessionMetadata
{
    public int DayOfStudy { get; set; }
    public int WeekOfStudy { get; set; }
    public bool IsRescheduled { get; set; }
}

public class AI4NGTask
{
    public string Id { get; set; } = string.Empty;
    public TaskData Data { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class TaskData
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty;
    public string? QuestionnaireId { get; set; }
    public List<string>? QuestionnaireIds { get; set; }
    public Dictionary<string, object> Configuration { get; set; } = new();
    public int EstimatedDuration { get; set; }
}

public class CreateSessionRequest
{
    public string ExperimentId { get; set; } = string.Empty;
    public string SessionType { get; set; } = string.Empty;
    public string SessionName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
}

public class CreateTaskRequest
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, object> Configuration { get; set; } = new();
    public int EstimatedDuration { get; set; }
}

public class MemberRequest
{
    public string Role { get; set; } = string.Empty;
}