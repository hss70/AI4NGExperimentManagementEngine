namespace AI4NGExperimentsLambda.Models.Dtos;

public class ExperimentDto
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public ExperimentData Data { get; set; } = new();
    public QuestionnaireConfig QuestionnaireConfig { get; set; } = new();
    public string? UpdatedAt { get; set; }
}
