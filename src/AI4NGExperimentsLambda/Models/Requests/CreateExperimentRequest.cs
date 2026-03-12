namespace AI4NGExperimentsLambda.Models.Requests;

public sealed class CreateExperimentRequest
{
    public string Id { get; set; } = string.Empty;
    public ExperimentData Data { get; init; } = new();
}