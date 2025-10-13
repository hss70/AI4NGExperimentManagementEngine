using Microsoft.AspNetCore.Http;
using System.IdentityModel.Tokens.Jwt;

namespace AI4NGExperimentManagement.Shared;

public class AuthenticationService : IAuthenticationService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthenticationService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetUsernameFromRequest()
    {
        var context = _httpContextAccessor.HttpContext ?? throw new InvalidOperationException("HttpContext not available");
        
        // For local testing, return a test user
        if (Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL") != null)
        {
            return "testuser";
        }

        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader))
            throw new UnauthorizedAccessException("Authorization header is required");

        if (!authHeader.StartsWith("Bearer "))
            throw new UnauthorizedAccessException("Bearer token required");

        var token = authHeader.Substring(7);
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            var username = jwtToken.Claims.FirstOrDefault(c => c.Type == "username" || c.Type == "cognito:username")?.Value;
            
            if (string.IsNullOrEmpty(username))
                throw new UnauthorizedAccessException("Invalid token: no username claim");
            
            return username;
        }
        catch (Exception ex) when (!(ex is UnauthorizedAccessException))
        {
            throw new UnauthorizedAccessException("Invalid token format");
        }
    }

    public bool IsResearcher()
    {
        var context = _httpContextAccessor.HttpContext ?? throw new InvalidOperationException("HttpContext not available");
        return context.Request.Path.StartsWithSegments("/api/researcher");
    }
}