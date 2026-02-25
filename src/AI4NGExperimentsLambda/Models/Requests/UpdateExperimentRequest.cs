namespace AI4NGExperimentsLambda.Models.Requests;

public sealed class UpdateExperimentRequest
{
    public ExperimentData Data { get; init; } = new();
    public QuestionnaireConfig QuestionnaireConfig { get; init; } = new();
}