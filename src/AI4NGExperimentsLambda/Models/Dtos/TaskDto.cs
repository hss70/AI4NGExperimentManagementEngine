namespace AI4NGExperimentsLambda.Models.Dtos;

public class TaskDto
{
    public string Id { get; set; } = string.Empty;
    public TaskData Data { get; set; } = new();
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

public class TaskDataDto
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public List<string> QuestionnaireIds { get; set; } = new();

    public Dictionary<string, object> Configuration { get; set; } = new();
    public int EstimatedDuration { get; set; }
}