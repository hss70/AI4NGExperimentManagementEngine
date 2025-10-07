using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace AI4NGExperimentsLambda;

public class LambdaEntryPoint
{
    private readonly AmazonDynamoDBClient _dynamoClient;
    private readonly string _experimentsTable;
    private readonly string _responsesTable;

    public LambdaEntryPoint()
    {
        _dynamoClient = new AmazonDynamoDBClient();
        _experimentsTable = Environment.GetEnvironmentVariable("EXPERIMENTS_TABLE") ?? "";
        _responsesTable = Environment.GetEnvironmentVariable("RESPONSES_TABLE") ?? "";
    }

    public async Task<APIGatewayProxyResponse> FunctionHandlerAsync(APIGatewayProxyRequest request, ILambdaContext context)
    {
        try
        {
            var username = GetUsernameFromJwt(GetJwtFromRequest(request));
            if (string.IsNullOrEmpty(username))
                return Error(401, "Unauthorized");

            return request.HttpMethod.ToUpper() switch
            {
                "GET" when request.PathParameters?.ContainsKey("experimentId") == true => 
                    await GetExperiment(request.PathParameters["experimentId"]),
                "GET" => await GetExperiments(),
                "POST" when request.Path.EndsWith("/sync") => 
                    await SyncExperiment(request.PathParameters?["experimentId"] ?? "", request.Body, username),
                "POST" => await CreateExperiment(request.Body, username),
                _ => Error(405, "Method not allowed")
            };
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error: {ex.Message}");
            return Error(500, ex.Message);
        }
    }

    private async Task<APIGatewayProxyResponse> GetExperiments()
    {
        var response = await _dynamoClient.ScanAsync(new ScanRequest
        {
            TableName = _experimentsTable,
            FilterExpression = "#type = :type",
            ExpressionAttributeNames = new Dictionary<string, string> { ["#type"] = "type" },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":type"] = new("Experiment") }
        });

        var experiments = response.Items.Select(item => new
        {
            id = item["PK"].S.Replace("EXPERIMENT#", ""),
            name = item["data"].M["name"].S,
            description = item["data"].M.GetValueOrDefault("description")?.S ?? ""
        });

        return Success(experiments);
    }

    private async Task<APIGatewayProxyResponse> GetExperiment(string experimentId)
    {
        var response = await _dynamoClient.QueryAsync(new QueryRequest
        {
            TableName = _experimentsTable,
            KeyConditionExpression = "PK = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"EXPERIMENT#{experimentId}")
            }
        });

        if (!response.Items.Any())
            return Error(404, "Experiment not found");

        var experiment = response.Items.First(i => i["SK"].S == "METADATA");
        var sessions = response.Items.Where(i => i["SK"].S.StartsWith("SESSION#")).ToList();

        return Success(new
        {
            id = experimentId,
            data = JsonSerializer.Deserialize<object>(experiment["data"].M.ToJson()),
            questionnaireConfig = JsonSerializer.Deserialize<object>(experiment["questionnaireConfig"].M.ToJson()),
            sessions = sessions.Select(s => JsonSerializer.Deserialize<object>(s["data"].M.ToJson()))
        });
    }

    private async Task<APIGatewayProxyResponse> CreateExperiment(string body, string username)
    {
        var experiment = JsonSerializer.Deserialize<JsonElement>(body);
        var experimentId = Guid.NewGuid().ToString();

        await _dynamoClient.PutItemAsync(new PutItemRequest
        {
            TableName = _experimentsTable,
            Item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"EXPERIMENT#{experimentId}"),
                ["SK"] = new("METADATA"),
                ["type"] = new("Experiment"),
                ["data"] = new { M = JsonToAttributeValue(experiment.GetProperty("data")) },
                ["questionnaireConfig"] = new { M = JsonToAttributeValue(experiment.GetProperty("questionnaireConfig")) },
                ["createdBy"] = new(username),
                ["createdAt"] = new(DateTime.UtcNow.ToString("O"))
            }
        });

        return Success(new { id = experimentId });
    }

    private async Task<APIGatewayProxyResponse> SyncExperiment(string experimentId, string body, string username)
    {
        var syncData = JsonSerializer.Deserialize<JsonElement>(body);
        var sessions = syncData.GetProperty("sessions").EnumerateArray();

        foreach (var session in sessions)
        {
            var sessionId = session.GetProperty("sessionId").GetString();
            await _dynamoClient.PutItemAsync(new PutItemRequest
            {
                TableName = _experimentsTable,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new($"EXPERIMENT#{experimentId}"),
                    ["SK"] = new($"SESSION#{sessionId}"),
                    ["type"] = new("Session"),
                    ["data"] = new { M = JsonToAttributeValue(session) },
                    ["GSI1PK"] = new($"EXPERIMENT#{experimentId}"),
                    ["GSI1SK"] = new($"SESSION#{sessionId}"),
                    ["updatedBy"] = new(username),
                    ["updatedAt"] = new(DateTime.UtcNow.ToString("O"))
                }
            });
        }

        return Success(new { message = "Experiment synced successfully" });
    }

    private Dictionary<string, AttributeValue> JsonToAttributeValue(JsonElement element)
    {
        var result = new Dictionary<string, AttributeValue>();
        foreach (var prop in element.EnumerateObject())
        {
            result[prop.Name] = prop.Value.ValueKind switch
            {
                JsonValueKind.String => new(prop.Value.GetString()),
                JsonValueKind.Number => new { N = prop.Value.GetDecimal().ToString() },
                _ => new(prop.Value.ToString())
            };
        }
        return result;
    }

    private string GetUsernameFromJwt(string token)
    {
        if (string.IsNullOrEmpty(token)) return "";
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            return jwtToken.Claims.FirstOrDefault(c => c.Type == "username" || c.Type == "cognito:username")?.Value ?? "";
        }
        catch { return ""; }
    }

    private string GetJwtFromRequest(APIGatewayProxyRequest request)
    {
        var authHeader = request.Headers?.FirstOrDefault(h => 
            h.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase)).Value;
        return authHeader?.StartsWith("Bearer ") == true ? authHeader.Substring(7) : "";
    }

    private APIGatewayProxyResponse Success(object data) => new()
    {
        StatusCode = 200,
        Body = JsonSerializer.Serialize(data),
        Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" }
    };

    private APIGatewayProxyResponse Error(int status, string message) => new()
    {
        StatusCode = status,
        Body = JsonSerializer.Serialize(new { error = message }),
        Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" }
    };
}