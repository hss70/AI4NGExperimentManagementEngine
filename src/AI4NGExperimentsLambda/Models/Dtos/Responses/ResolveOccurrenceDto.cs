namespace AI4NGExperimentsLambda.Models.Dtos.Responses;

public sealed class ResolveOccurrenceDto
{
    public string ResolutionType { get; init; } = string.Empty;
    // resume_existing / start_required / start_optional / none_available

    public OccurrenceDto? Occurrence { get; init; }

    public List<OccurrenceActionDto> AvailableActions { get; init; } = new();
}

public sealed class OccurrenceActionDto
{
    public string ActionKey { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string OccurrenceType { get; init; } = string.Empty;
    public string SessionTypeKey { get; init; } = string.Empty;
    public bool IsRequired { get; init; }
}