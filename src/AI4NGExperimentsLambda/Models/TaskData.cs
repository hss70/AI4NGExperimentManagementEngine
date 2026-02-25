namespace AI4NGExperimentsLambda.Models;

public class TaskData
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public List<string> QuestionnaireIds { get; set; } = new();

    public Dictionary<string, object> Configuration { get; set; } = new();
    public int EstimatedDuration { get; set; }
}
