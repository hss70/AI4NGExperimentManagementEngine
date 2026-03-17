namespace AI4NGExperimentsLambda.Models;

public sealed class TaskResponse
{
    public string ResponseId { get; set; } = string.Empty;
    public string ExperimentId { get; set; } = string.Empty;
    public string ParticipantId { get; set; } = string.Empty;
    public string OccurrenceKey { get; set; } = string.Empty;
    public string TaskKey { get; set; } = string.Empty;
    public string? QuestionnaireId { get; set; }
    public object Payload { get; set; } = new();
    public string ClientSubmissionId { get; set; } = string.Empty;
    public string? ClientSubmittedAt { get; set; }
    public string SubmittedAt { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
}
