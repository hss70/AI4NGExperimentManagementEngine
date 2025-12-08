namespace AI4NGExperimentsLambda.Models.Dtos;

public class ExperimentSyncDto
{
    public ExperimentDto? Experiment { get; set; }
    public List<SessionDto> Sessions { get; set; } = new();
    public List<TaskDto> Tasks { get; set; } = new();
    public List<string> Questionnaires { get; set; } = new();
    public List<string> SessionNames { get; set; } = new();
    public List<string> SessionTypes { get; set; } = new();
    public string SyncTimestamp { get; set; } = string.Empty;
}

public class ExperimentDto
{
    public string Id { get; set; } = string.Empty;
    public ExperimentData Data { get; set; } = new();
    public QuestionnaireConfig QuestionnaireConfig { get; set; } = new();
    public string? UpdatedAt { get; set; }
}

public class ExperimentListDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Role { get; set; }
}

public class SessionDto
{
    public string? SessionId { get; set; }
    public string? ExperimentId { get; set; }
    public SessionData Data { get; set; } = new();
    public List<string> TaskOrder { get; set; } = new();
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
}

public class TaskDto
{
    public string Id { get; set; } = string.Empty;
    public TaskData Data { get; set; } = new();
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
}

public class MemberDto
{
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = "participant";
    public string Status { get; set; } = "active";
    public string Cohort { get; set; } = string.Empty;
    public string? JoinedAt { get; set; }
}