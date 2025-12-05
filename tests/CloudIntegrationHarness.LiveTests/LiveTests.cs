using System.Net.Http.Headers;
using CloudIntegrationHarness;
using Xunit;

namespace CloudIntegrationHarness.LiveTests;

public class LiveTests
{
    [Fact]
    public async Task CloudHarness_RunAsync_HitsLiveApi_AndCompletes()
    {
        var cfg = HarnessConfig.Load();
        Assert.False(string.IsNullOrWhiteSpace(cfg.ApiBaseUrl));

        // Acquire JWTs for researcher and participant via Cognito
        var cognito = new CognitoAuthClient();
        var researcherJwt = await cognito.GetIdTokenAsync("eu-west-2", cfg.CognitoClientId, cfg.ResearcherUsername, cfg.ResearcherPassword);
        var participantJwt = await cognito.GetIdTokenAsync("eu-west-2", cfg.CognitoClientId, cfg.ParticipantUsername, cfg.ParticipantPassword);
        Assert.False(string.IsNullOrWhiteSpace(researcherJwt));
        Assert.False(string.IsNullOrWhiteSpace(participantJwt));

        using var http = new HttpClient { BaseAddress = new Uri(cfg.ApiBaseUrl) };

        var harness = new CloudHarness(http, cfg);

        await harness.RunAsync(
            setAuth: token => http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token),
            researcherJwt: researcherJwt,
            participantJwt: participantJwt
        );
    }
}