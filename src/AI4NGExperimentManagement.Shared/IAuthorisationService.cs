namespace AI4NGExperimentManagement.Shared.Authorisation;

public interface IAuthorisationService
{
    // Throws UnauthorizedAccessException / KeyNotFoundException / InvalidOperationException
    Task EnsureExperimentMemberAsync(
        string experimentId,
        string participantId,
        CancellationToken ct = default);

    Task EnsureExperimentRoleAsync(
        string experimentId,
        string participantId,
        string requiredRole,
        CancellationToken ct = default);

    // Returns null if not a member (caller can decide what to do)
    Task<ExperimentMembership?> GetMembershipAsync(
        string experimentId,
        string participantId,
        CancellationToken ct = default);
}

public class ExperimentMembership
{
    public string ExperimentId { get; init; } = string.Empty;
    public string ParticipantId { get; init; } = string.Empty;
    public string Role { get; init; } = "participant";
    public string Status { get; init; } = "active";
    public string? Cohort
    {
        get; init;
    }
}