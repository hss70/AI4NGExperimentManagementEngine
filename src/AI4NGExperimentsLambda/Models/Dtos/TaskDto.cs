namespace AI4NGExperimentsLambda.Models.Dtos;

public class TaskDto
{
    public string Id { get; set; } = string.Empty;
    public TaskData Data { get; set; } = new();
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
}
