using Microsoft.AspNetCore.Http;
using System.IdentityModel.Tokens.Jwt;

namespace AI4NGExperimentManagement.Shared;

public static class AuthHelper
{
    public static string? GetUsernameFromJwt(HttpRequest request)
    {
        // For local testing, return a test user
        if (Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL") != null)
            return "testuser";

        var authHeader = request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader))
            throw new UnauthorizedAccessException("Authorization header is required");

        if (!authHeader.StartsWith("Bearer "))
            throw new UnauthorizedAccessException("Bearer token required");

        var token = authHeader["Bearer ".Length..];
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);
            var username = jwt.Claims.FirstOrDefault(c => c.Type == "cognito:username")?.Value;
            
            if (string.IsNullOrEmpty(username))
                throw new UnauthorizedAccessException("Invalid token: no username claim");
            
            return username;
        }
        catch (Exception ex) when (!(ex is UnauthorizedAccessException))
        {
            throw new UnauthorizedAccessException("Invalid token format");
        }
    }

    public static bool IsResearcher(HttpRequest request)
    {
        return request.Path.StartsWithSegments("/api/researcher");
    }

    public static void LogDebug(HttpRequest request, string message, string controllerName)
    {
        if (request.Headers.ContainsKey("X-Debug") || Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL") != null)
        {
            Console.WriteLine($"[DEBUG] {controllerName}: {message}");
        }
    }
}