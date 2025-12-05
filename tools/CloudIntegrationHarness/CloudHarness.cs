using System.Net.Http.Json;
using System.Text.Json;

namespace CloudIntegrationHarness;

public class CloudHarness
{
    private readonly HttpClient _http;
    private readonly HarnessConfig _cfg;
    private readonly string _baseUrl;

    public CloudHarness(HttpClient http, HarnessConfig cfg)
    {
        _http = http;
        _cfg = cfg;
        _baseUrl = cfg.ApiBaseUrl.EndsWith("/") ? cfg.ApiBaseUrl : cfg.ApiBaseUrl + "/";
    }

    public async Task RunAsync(Action<string> setAuth, string researcherJwt, string participantJwt)
    {
        Console.WriteLine($"[INIT] HttpClient BaseAddress = {_http.BaseAddress}");
        // Researcher auth for creation/membership management
        setAuth(researcherJwt);

        // Sanity check: experiments endpoint reachable (non-blocking)
        await PreflightCheckAsync();

        // 1) Create an experiment with two questionnaires
        string experimentId;
        try
        {
            experimentId = await CreateExperimentAsync();
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[WARN] Create with researcher failed: {ex.Message}. Retrying with participant token...");
            setAuth(participantJwt);
            experimentId = await CreateExperimentAsync();
        }

        // 2) Negative check: participant should NOT see experiment before membership
        setAuth(participantJwt);
        await EnsureMyExperimentsContainsAsync(experimentId, expectedContains: false);

        // 3) Add a member (requires researcher)
        setAuth(researcherJwt);
        await AddMemberAsync(experimentId, _cfg.ParticipantUsername);

        // 4) Switch to participant and verify they now see the experiment
        setAuth(participantJwt);
        await EnsureMyExperimentsContainsAsync(experimentId, expectedContains: true);
        var responseId1 = await SubmitResponseAsync(experimentId, _cfg.ParticipantUsername, _cfg.QuestionnaireIdPQ, new { score = 42, answers = new[] { 1, 2, 3 } });
        var responseId2 = await SubmitResponseAsync(experimentId, _cfg.ParticipantUsername, _cfg.QuestionnaireIdATI, new { score = 15, items = new[] { 3, 2, 1 } });

        // 5) Verify retrieval
        await VerifyResponsesAsync(experimentId, _cfg.ParticipantUsername, responseId1, responseId2);

        // 6) Cleanup: delete responses and experiment
        await DeleteResponseAsync(responseId1);
        await DeleteResponseAsync(responseId2);
        // Back to researcher for cleanup
        setAuth(researcherJwt);
        await DeleteExperimentAsync(experimentId);
    }

    private async Task<string> CreateExperimentAsync()
    {
        var expId = $"EXP-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var body = new
        {
            id = expId,
            data = new
            {
                name = $"harness-exp-{DateTime.UtcNow:yyyyMMddHHmmss}",
                description = "Harness experiment",
                questionnaireIds = new[] { _cfg.QuestionnaireIdPQ, _cfg.QuestionnaireIdATI },
                status = "active"
            }
        };

        var target = new Uri($"{_baseUrl}api/experiments");
        Console.WriteLine($"[POST] api/experiments -> full URL: {target}");
        Console.WriteLine("[POST] api/experiments body=" + System.Text.Json.JsonSerializer.Serialize(body));
        var resp = await _http.PostAsJsonAsync(target, body);
        Console.WriteLine($"[POST] requested URI: {resp.RequestMessage!.RequestUri}");
        Console.WriteLine($"[POST] api/experiments -> {(int)resp.StatusCode} {resp.ReasonPhrase}");
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Console.WriteLine("[DEBUG] me/experiments JSON=" + System.Text.Json.JsonSerializer.Serialize(json));
        // Prefer returned id if present; otherwise use the one we sent
        var returnedId = json.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
        return returnedId ?? expId;
    }

    private async Task AddMemberAsync(string experimentId, string userSub)
    {
        var body = new { role = "participant", status = "active" };
        Console.WriteLine($"[PUT] api/experiments/{experimentId}/members/{userSub} body=" + System.Text.Json.JsonSerializer.Serialize(body));
        var target = new Uri($"{_baseUrl}api/experiments/{experimentId}/members/{userSub}");
        var resp = await _http.PutAsJsonAsync(target, body);
        Console.WriteLine($"[PUT] api/experiments/{experimentId}/members/{userSub} -> {(int)resp.StatusCode} {resp.ReasonPhrase}");
        resp.EnsureSuccessStatusCode();
    }

    private async Task<string> SubmitResponseAsync(string experimentId, string sessionId, string questionnaireId, object data)
    {
        var body = new
        {
            experimentId,
            sessionId,
            questionnaireId,
            data
        };
        Console.WriteLine("[POST] api/responses body=" + System.Text.Json.JsonSerializer.Serialize(body));
        var target = new Uri($"{_baseUrl}api/responses");
        var resp = await _http.PostAsJsonAsync(target, body);
        Console.WriteLine($"[POST] api/responses -> {(int)resp.StatusCode} {resp.ReasonPhrase}");
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("id").GetString()!;
    }

    private async Task VerifyResponsesAsync(string experimentId, string sessionId, string r1, string r2)
    {
        // List all for experiment+session
        Console.WriteLine($"[GET] api/responses?experimentId={experimentId}&sessionId={sessionId}");
        var listTarget = new Uri($"{_baseUrl}api/responses?experimentId={experimentId}&sessionId={sessionId}");
        var listResp = await _http.GetAsync(listTarget);
        Console.WriteLine($"[GET] api/responses -> {(int)listResp.StatusCode} {listResp.ReasonPhrase}");
        listResp.EnsureSuccessStatusCode();
        var listJson = await listResp.Content.ReadFromJsonAsync<JsonElement>();

        // GetById for each
        Console.WriteLine($"[GET] api/responses/{r1}");
        var get1Target = new Uri($"{_baseUrl}api/responses/{r1}");
        var get1 = await _http.GetAsync(get1Target);
        Console.WriteLine($"[GET] api/responses/{r1} -> {(int)get1.StatusCode} {get1.ReasonPhrase}");
        get1.EnsureSuccessStatusCode();
        Console.WriteLine($"[GET] api/responses/{r2}");
        var get2Target = new Uri($"{_baseUrl}api/responses/{r2}");
        var get2 = await _http.GetAsync(get2Target);
        Console.WriteLine($"[GET] api/responses/{r2} -> {(int)get2.StatusCode} {get2.ReasonPhrase}");
        get2.EnsureSuccessStatusCode();
    }

    private async Task DeleteResponseAsync(string responseId)
    {
        Console.WriteLine($"[DELETE] api/responses/{responseId}");
        var target = new Uri($"{_baseUrl}api/responses/{responseId}");
        var resp = await _http.DeleteAsync(target);
        Console.WriteLine($"[DELETE] api/responses/{responseId} -> {(int)resp.StatusCode} {resp.ReasonPhrase}");
        resp.EnsureSuccessStatusCode();
    }

    private async Task DeleteExperimentAsync(string experimentId)
    {
        Console.WriteLine($"[DELETE] api/experiments/{experimentId}");
        var target = new Uri($"{_baseUrl}api/experiments/{experimentId}");
        var resp = await _http.DeleteAsync(target);
        Console.WriteLine($"[DELETE] api/experiments/{experimentId} -> {(int)resp.StatusCode} {resp.ReasonPhrase}");
        resp.EnsureSuccessStatusCode();
    }

    private async Task EnsureMyExperimentsContainsAsync(string experimentId, bool expectedContains)
    {
        var target = new Uri($"{_baseUrl}api/me/experiments");
        Console.WriteLine($"[GET] api/me/experiments -> full URL: {target}");
        var resp = await _http.GetAsync(target);
        Console.WriteLine($"[GET] requested URI: {resp.RequestMessage!.RequestUri}");
        Console.WriteLine($"[GET] api/me/experiments -> {(int)resp.StatusCode} {resp.ReasonPhrase}");
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Console.WriteLine("[DEBUG] me/experiments JSON=" + System.Text.Json.JsonSerializer.Serialize(json));

        bool contains = false;
        if (json.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in json.EnumerateArray())
            {
                if (TryMatchExperimentId(item, experimentId)) { contains = true; break; }
            }
        }
        else if (json.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                if (TryMatchExperimentId(item, experimentId)) { contains = true; break; }
            }
        }
        else
        {
            // Single object or unexpected shape fallback
            contains = TryMatchExperimentId(json, experimentId);
        }

        Console.WriteLine($"[ASSERT] me/experiments contains '{experimentId}': {contains} (expected {expectedContains})");
        if (contains != expectedContains)
        {
            throw new InvalidOperationException($"me/experiments contains check failed for '{experimentId}': expected {expectedContains}, got {contains}");
        }
    }

    private static bool TryMatchExperimentId(JsonElement element, string experimentId)
    {
        // Common shapes: { id: "EXP...", data: {...} } or nested
        if (element.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
        {
            if (string.Equals(idProp.GetString(), experimentId, StringComparison.Ordinal)) return true;
        }
        // Some shapes: { experimentId: "EXP...", ... }
        if (element.TryGetProperty("experimentId", out var expIdProp) && expIdProp.ValueKind == JsonValueKind.String)
        {
            if (string.Equals(expIdProp.GetString(), experimentId, StringComparison.Ordinal)) return true;
        }
        // Sometimes id can be nested under experiment object
        if (element.TryGetProperty("experiment", out var exp) && exp.ValueKind == JsonValueKind.Object)
        {
            if (exp.TryGetProperty("id", out var nestedId) && nestedId.ValueKind == JsonValueKind.String)
            {
                if (string.Equals(nestedId.GetString(), experimentId, StringComparison.Ordinal)) return true;
            }
        }
        return false;
    }

    private async Task PreflightCheckAsync()
    {
        var target = new Uri($"{_baseUrl}api/experiments");
        Console.WriteLine($"[GET] api/experiments (preflight) -> full URL: {target}");
        var resp = await _http.GetAsync(target);
        Console.WriteLine($"[GET] requested URI: {resp.RequestMessage!.RequestUri}");
        Console.WriteLine($"[GET] api/experiments -> {(int)resp.StatusCode} {resp.ReasonPhrase}");
        // Non-blocking: do not fail if preflight returns 404; creation may still be allowed
    }
}