
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using AI4NGExperimentsLambda.Models.Dtos;
using AI4NGExperimentsLambda.Interfaces.Researcher;

namespace AI4NGExperimentsLambda.Services.Researcher;

public sealed class UserLookupService : IUserLookupService
{
    private readonly IAmazonCognitoIdentityProvider _cognito;
    private readonly string _userPoolId;

    public UserLookupService(IAmazonCognitoIdentityProvider cognito)
    {
        _cognito = cognito;
        _userPoolId = Environment.GetEnvironmentVariable("COGNITO_USER_POOL_ID") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(_userPoolId))
            throw new InvalidOperationException("COGNITO_USER_POOL_ID environment variable is not set");
    }

    public async Task<UserLookupDto?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        email = (email ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required");

        var response = await _cognito.ListUsersAsync(new ListUsersRequest
        {
            UserPoolId = _userPoolId,
            Filter = $"email = \"{EscapeFilterValue(email)}\"",
            Limit = 1
        }, ct);

        var user = response.Users.FirstOrDefault();
        return user == null ? null : MapUser(user);
    }

    public async Task<UserLookupDto?> GetByUsernameAsync(string username, CancellationToken ct = default)
    {
        username = (username ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username is required");

        try
        {
            var response = await _cognito.AdminGetUserAsync(new AdminGetUserRequest
            {
                UserPoolId = _userPoolId,
                Username = username
            }, ct);

            return MapAdminUser(response);
        }
        catch (UserNotFoundException)
        {
            return null;
        }
    }

    private static UserLookupDto MapUser(UserType user)
    {
        var attrs = ToDictionary(user.Attributes);

        return new UserLookupDto
        {
            UserSub = GetAttr(attrs, "sub"),
            Username = user.Username ?? string.Empty,
            Email = GetAttr(attrs, "email"),
            Enabled = user.Enabled ?? false,
            UserStatus = user.UserStatus?.Value
        };
    }

    private static UserLookupDto MapAdminUser(AdminGetUserResponse user)
    {
        var attrs = ToDictionary(user.UserAttributes);

        return new UserLookupDto
        {
            UserSub = GetAttr(attrs, "sub"),
            Username = user.Username ?? string.Empty,
            Email = GetAttr(attrs, "email"),
            Enabled = user.Enabled,
            UserStatus = user.UserStatus?.Value
        };
    }

    private static Dictionary<string, string> ToDictionary(List<AttributeType>? attributes)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (attributes == null)
            return dict;

        foreach (var attr in attributes)
        {
            if (!string.IsNullOrWhiteSpace(attr.Name))
                dict[attr.Name] = attr.Value ?? string.Empty;
        }

        return dict;
    }

    private static string? GetAttr(Dictionary<string, string> attributes, string key)
        => attributes.TryGetValue(key, out var value) ? value : null;

    private static string EscapeFilterValue(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}