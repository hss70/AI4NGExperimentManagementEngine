namespace AI4NGExperimentsLambda.Models.Requests;

public sealed class UpdateExperimentRequest
{
    public ExperimentDataPatch? Data { get; set; }
}

public sealed class ExperimentDataPatch
{
    public string? Name { get; init; }
    public string? Description { get; init; }

    public string? StudyStartDate { get; init; }
    public string? StudyEndDate { get; init; }

    public int? ParticipantDurationDays { get; init; }

    public Dictionary<string, SessionType>? SessionTypes { get; init; }
}