namespace AI4NGExperimentsLambda.Models.Dtos;

public sealed class ValidateExperimentResponseDto
{
    public bool Valid
    {
        get; init;
    }
    public List<string> ReferencedQuestionnaires { get; init; } = new();
    public List<string> MissingQuestionnaires { get; init; } = new();
    public string Message { get; init; } = string.Empty;
}