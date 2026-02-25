namespace AI4NGExperimentsLambda.Models;

public class CreateTaskRequest
{
    public string TaskKey { get; set; } = string.Empty;
    public TaskData Data { get; set; } = new();
}
