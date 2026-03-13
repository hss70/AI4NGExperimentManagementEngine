namespace AI4NGExperimentManagement.Shared;

public interface IAuthenticationService
{
    string GetUsernameFromRequest();
    string GetUserSubFromRequest();
    bool IsResearcher();
    bool IsParticipant();
}