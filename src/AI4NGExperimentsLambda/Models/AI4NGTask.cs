namespace AI4NGExperimentsLambda.Models;

public class AI4NGTask
{
    public string TaskKey { get; set; } = string.Empty;
    public TaskData Data { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
