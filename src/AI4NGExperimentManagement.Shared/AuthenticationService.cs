using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace AI4NGExperimentManagement.Shared;

public class AuthenticationService : IAuthenticationService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    private ClaimsPrincipal GetUser() =>
        _httpContextAccessor.HttpContext?.User
        ?? throw new InvalidOperationException("User context not available");

    public AuthenticationService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetUsernameFromRequest()
    {
        var user = GetUser();

        // ✅ For local testing
        if (Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL") != null)
            return "testuser";

        var username = user.FindFirst("cognito:username")?.Value
                    ?? user.FindFirst("username")?.Value
                    ?? user.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(username))
            throw new UnauthorizedAccessException("Authenticated user has no username claim");

        return username;
    }

    public bool IsResearcher()
    {
        var user = GetUser();

        return user.IsInRole("Researcher") ||
               user.Claims.Any(c => c.Type == "cognito:groups" && c.Value == "Researcher");
    }

    public bool IsParticipant()
    {
        var user = GetUser();

        return user.IsInRole("Participant") ||
               user.Claims.Any(c => c.Type == "cognito:groups" && (c.Value == "Researcher" || c.Value == "Participant"));
    }
}