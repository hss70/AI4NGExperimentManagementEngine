namespace AI4NGExperimentManagement.Shared;

public interface IAuthenticationService
{
    string GetUsernameFromRequest();
    bool IsResearcher();
}