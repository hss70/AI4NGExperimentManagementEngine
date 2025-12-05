using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace CloudIntegrationHarness;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("AI4NG Cloud Integration Harness");

        var config = HarnessConfig.Load();
        if (string.IsNullOrWhiteSpace(config.ApiBaseUrl))
        {
            Console.Error.WriteLine("Missing ApiBaseUrl in appsettings.json.");
            return 2;
        }

        // Obtain JWTs via Cognito for researcher and participant
        string researcherJwt = string.Empty;
        string participantJwt = string.Empty;
        if (config.UseCognitoAuth)
        {
            Console.WriteLine("Requesting JWTs via Cognito InitiateAuth (Researcher & Participant)...");
            var cognito = new CognitoAuthClient();
            researcherJwt = await cognito.GetIdTokenAsync(
                region: "eu-west-2",
                clientId: config.CognitoClientId,
                username: config.ResearcherUsername,
                password: config.ResearcherPassword
            );
            participantJwt = await cognito.GetIdTokenAsync(
                region: "eu-west-2",
                clientId: config.CognitoClientId,
                username: config.ParticipantUsername,
                password: config.ParticipantPassword
            );
            Console.WriteLine("Obtained IdTokens.");
        }

        // Ensure trailing slash in BaseAddress so relative paths are appended after '/dev/'
        var baseUrl = config.ApiBaseUrl.EndsWith("/") ? config.ApiBaseUrl : config.ApiBaseUrl + "/";
        using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };
        var harness = new CloudHarness(http, config);

        try
        {
            await harness.RunAsync(
                setAuth: token => http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token),
                researcherJwt: researcherJwt,
                participantJwt: participantJwt
            );
            Console.WriteLine("Harness completed successfully.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Harness failed: {ex}");
            return 1;
        }
    }
}

public record HarnessConfig(
    string ApiBaseUrl,
    bool UseCognitoAuth,
    string CognitoClientId,
    string ResearcherUsername,
    string ResearcherPassword,
    string ParticipantUsername,
    string ParticipantPassword,
    string ParticipantSub,
    string QuestionnaireIdPQ,
    string QuestionnaireIdATI
)
{
    public static HarnessConfig Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var json = File.ReadAllText(path);
        var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;
        return new HarnessConfig(
            root.GetProperty("ApiBaseUrl").GetString()!,
            root.TryGetProperty("UseCognitoAuth", out var useCognito) && useCognito.GetBoolean(),
            root.TryGetProperty("CognitoClientId", out var clientId) ? clientId.GetString() ?? string.Empty : string.Empty,
            root.GetProperty("Researcher").GetProperty("Username").GetString()!,
            root.GetProperty("Researcher").GetProperty("Password").GetString()!,
            root.GetProperty("Participant").GetProperty("Username").GetString()!,
            root.GetProperty("Participant").GetProperty("Password").GetString()!,
            root.GetProperty("Participant").GetProperty("Sub").GetString()!,
            root.GetProperty("QuestionnaireIdPQ").GetString()!,
            root.GetProperty("QuestionnaireIdATI").GetString()!
        );
    }
}