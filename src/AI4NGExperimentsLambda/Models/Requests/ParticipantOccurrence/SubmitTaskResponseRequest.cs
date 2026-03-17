namespace AI4NGExperimentsLambda.Models.Requests.Participant;

public sealed class SubmitTaskResponseRequest
{
    public string ClientSubmissionId { get; init; } = string.Empty;
    public string? ClientSubmittedAt { get; init; }
    public string? QuestionnaireId { get; init; }
    public object Payload { get; init; } = default!;
}
