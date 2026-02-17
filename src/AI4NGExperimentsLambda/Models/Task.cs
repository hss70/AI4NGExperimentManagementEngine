namespace AI4NGExperimentsLambda.Models;

public class CreateTaskRequest
{
    public string TaskKey { get; set; } = string.Empty;
    public TaskData Data { get; set; } = new();
}

public class AI4NGTask
{
    public string TaskKey { get; set; } = string.Empty;
    public TaskData Data { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class TaskData
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public List<string> QuestionnaireIds { get; set; } = new();

    public Dictionary<string, object> Configuration { get; set; } = new();
    public int EstimatedDuration { get; set; }
}
