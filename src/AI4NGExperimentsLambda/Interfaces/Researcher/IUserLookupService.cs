namespace AI4NGExperimentsLambda.Interfaces.Researcher;

using AI4NGExperimentsLambda.Models.Dtos;

public interface IUserLookupService
{
    Task<UserLookupDto?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<UserLookupDto?> GetByUsernameAsync(string username, CancellationToken ct = default);
}
