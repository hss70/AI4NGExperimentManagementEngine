using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CloudIntegrationHarness;

public class CognitoAuthClient
{
    private static readonly Uri Endpoint = new("https://cognito-idp.eu-west-2.amazonaws.com");

    public async Task<string> GetIdTokenAsync(string region, string clientId, string username, string password)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Cognito credentials are required.");

        using var http = new HttpClient { BaseAddress = Endpoint };
        using var req = new HttpRequestMessage(HttpMethod.Post, "")
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                AuthFlow = "USER_PASSWORD_AUTH",
                ClientId = clientId,
                AuthParameters = new
                {
                    USERNAME = username,
                    PASSWORD = password
                }
            }), Encoding.UTF8, "application/x-amz-json-1.1")
        };
        req.Headers.Add("X-Amz-Target", "AWSCognitoIdentityProviderService.InitiateAuth");

        var resp = await http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        using var stream = await resp.Content.ReadAsStreamAsync();
        var doc = await JsonDocument.ParseAsync(stream);
        var root = doc.RootElement;
        var authResult = root.GetProperty("AuthenticationResult");
        var idToken = authResult.GetProperty("IdToken").GetString();
        if (string.IsNullOrWhiteSpace(idToken))
            throw new InvalidOperationException("Cognito response missing IdToken.");
        return idToken!;
    }
}