namespace AI4NGExperimentManagement.Shared.Authorisation;

public class AuthorisationService : IAuthorisationService
{
    public Task EnsureExperimentMemberAsync(string experimentId, string participantId, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task EnsureExperimentRoleAsync(string experimentId, string participantId, string requiredRole, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<ExperimentMembership?> GetMembershipAsync(string experimentId, string participantId, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}
