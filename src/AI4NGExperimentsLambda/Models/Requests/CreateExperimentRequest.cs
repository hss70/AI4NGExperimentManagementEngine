namespace AI4NGExperimentsLambda.Models.Requests;

public sealed class CreateExperimentRequest
{
    public ExperimentData Data { get; init; } = new();
}