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

            var isResearcher = request.Path.StartsWith("/api/researcher/");
            
            return request.HttpMethod.ToUpper() switch
            {
                "GET" when request.Path == "/api/me/experiments" => await GetMyExperiments(username),
                "GET" when request.Path.Contains("/members") => await GetExperimentMembers(request.PathParameters?["experimentId"] ?? ""),
                "GET" when request.PathParameters?.ContainsKey("experimentId") == true => 
                    await GetExperiment(request.PathParameters["experimentId"]),
                "GET" => await GetExperiments(),
                "POST" when request.Path.EndsWith("/sync") => 
                    await SyncExperiment(request.PathParameters?["experimentId"] ?? "", request.Body, username),
                "PUT" when request.Path.Contains("/members/") => 
                    await AddMember(request.PathParameters?["experimentId"] ?? "", request.PathParameters?["userSub"] ?? "", request.Body, username),
                "DELETE" when request.Path.Contains("/members/") => 
                    await RemoveMember(request.PathParameters?["experimentId"] ?? "", request.PathParameters?["userSub"] ?? "", username),
                "POST" when isResearcher => await CreateExperiment(request.Body, username),
                "PUT" when isResearcher => await UpdateExperiment(request.PathParameters?["experimentId"] ?? "", request.Body, username),
                "DELETE" when isResearcher => await DeleteExperiment(request.PathParameters?["experimentId"] ?? "", username),
                "POST" when !isResearcher => Error(403, "Participants cannot create experiments"),
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
            data = ConvertAttributeValueToObject(experiment["data"]),
            questionnaireConfig = ConvertAttributeValueToObject(experiment["questionnaireConfig"]),
            sessions = sessions.Select(s => ConvertAttributeValueToObject(s["data"]))
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
                ["data"] = new AttributeValue { M = JsonToAttributeValue(experiment.GetProperty("data")) },
                ["questionnaireConfig"] = new AttributeValue { M = JsonToAttributeValue(experiment.GetProperty("questionnaireConfig")) },
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
                    ["data"] = new AttributeValue { M = JsonToAttributeValue(session) },
                    ["GSI1PK"] = new($"EXPERIMENT#{experimentId}"),
                    ["GSI1SK"] = new($"SESSION#{sessionId}"),
                    ["updatedBy"] = new(username),
                    ["updatedAt"] = new(DateTime.UtcNow.ToString("O"))
                }
            });
        }

        return Success(new { message = "Experiment synced successfully" });
    }

    private async Task<APIGatewayProxyResponse> UpdateExperiment(string experimentId, string body, string username)
    {
        var experiment = JsonSerializer.Deserialize<JsonElement>(body);

        await _dynamoClient.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"EXPERIMENT#{experimentId}"),
                ["SK"] = new("METADATA")
            },
            UpdateExpression = "SET #data = :data, updatedBy = :user, updatedAt = :timestamp",
            ExpressionAttributeNames = new Dictionary<string, string> { ["#data"] = "data" },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":data"] = new AttributeValue { M = JsonToAttributeValue(experiment.GetProperty("data")) },
                [":user"] = new(username),
                [":timestamp"] = new(DateTime.UtcNow.ToString("O"))
            }
        });

        return Success(new { message = "Experiment updated successfully" });
    }

    private async Task<APIGatewayProxyResponse> DeleteExperiment(string experimentId, string username)
    {
        await _dynamoClient.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"EXPERIMENT#{experimentId}"),
                ["SK"] = new("METADATA")
            }
        });

        return Success(new { message = "Experiment deleted successfully" });
    }

    private async Task<APIGatewayProxyResponse> GetMyExperiments(string username)
    {
        var response = await _dynamoClient.QueryAsync(new QueryRequest
        {
            TableName = _experimentsTable,
            IndexName = "GSI1",
            KeyConditionExpression = "GSI1PK = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"USER#{username}")
            }
        });

        var experiments = new List<object>();
        foreach (var item in response.Items)
        {
            var experimentId = item["GSI1SK"].S.Replace("EXPERIMENT#", "");
            var experimentResponse = await _dynamoClient.GetItemAsync(new GetItemRequest
            {
                TableName = _experimentsTable,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new($"EXPERIMENT#{experimentId}"),
                    ["SK"] = new("METADATA")
                }
            });

            if (experimentResponse.IsItemSet)
            {
                experiments.Add(new
                {
                    id = experimentId,
                    name = experimentResponse.Item["data"].M["name"].S,
                    description = experimentResponse.Item["data"].M.GetValueOrDefault("description")?.S ?? "",
                    membership = new
                    {
                        role = item["role"].S,
                        status = item["status"].S,
                        cohort = item.GetValueOrDefault("cohort")?.S,
                        pseudoId = item.GetValueOrDefault("pseudoId")?.S
                    }
                });
            }
        }

        return Success(experiments);
    }

    private async Task<APIGatewayProxyResponse> GetExperimentMembers(string experimentId)
    {
        var response = await _dynamoClient.QueryAsync(new QueryRequest
        {
            TableName = _experimentsTable,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :sk)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"EXPERIMENT#{experimentId}"),
                [":sk"] = new("MEMBER#")
            }
        });

        var members = response.Items.Select(item => new
        {
            userSub = item["SK"].S.Replace("MEMBER#", ""),
            role = item["role"].S,
            status = item["status"].S,
            assignedAt = item["assignedAt"].S,
            cohort = item.GetValueOrDefault("cohort")?.S,
            pseudoId = item.GetValueOrDefault("pseudoId")?.S
        });

        return Success(members);
    }

    private async Task<APIGatewayProxyResponse> AddMember(string experimentId, string userSub, string body, string username)
    {
        var memberData = JsonSerializer.Deserialize<JsonElement>(body);
        var timestamp = DateTime.UtcNow.ToString("O");

        await _dynamoClient.PutItemAsync(new PutItemRequest
        {
            TableName = _experimentsTable,
            Item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"EXPERIMENT#{experimentId}"),
                ["SK"] = new($"MEMBER#{userSub}"),
                ["GSI1PK"] = new($"USER#{userSub}"),
                ["GSI1SK"] = new($"EXPERIMENT#{experimentId}"),
                ["type"] = new("Membership"),
                ["role"] = new(memberData.GetProperty("role").GetString()),
                ["status"] = new(memberData.GetProperty("status").GetString()),
                ["assignedAt"] = new(timestamp),
                ["cohort"] = new(memberData.GetProperty("cohort").GetString()),
                ["pseudoId"] = new(memberData.GetProperty("pseudoId").GetString()),
                ["assignedBy"] = new(username)
            }
        });

        return Success(new { message = "Member added successfully" });
    }

    private async Task<APIGatewayProxyResponse> RemoveMember(string experimentId, string userSub, string username)
    {
        await _dynamoClient.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _experimentsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"EXPERIMENT#{experimentId}"),
                ["SK"] = new($"MEMBER#{userSub}")
            }
        });

        return Success(new { message = "Member removed successfully" });
    }

    private Dictionary<string, AttributeValue> JsonToAttributeValue(JsonElement element)
    {
        var result = new Dictionary<string, AttributeValue>();
        foreach (var prop in element.EnumerateObject())
        {
            result[prop.Name] = prop.Value.ValueKind switch
            {
                JsonValueKind.String => new(prop.Value.GetString()),
                JsonValueKind.Number => new AttributeValue { N = prop.Value.GetDecimal().ToString() },
                _ => new(prop.Value.ToString())
            };
        }
        return result;
    }

    private object ConvertAttributeValueToObject(AttributeValue attributeValue)
    {
        if (attributeValue.M != null)
        {
            var dict = new Dictionary<string, object>();
            foreach (var kvp in attributeValue.M)
            {
                dict[kvp.Key] = ConvertAttributeValueToObject(kvp.Value);
            }
            return dict;
        }
        if (attributeValue.S != null) return attributeValue.S;
        if (attributeValue.N != null) return decimal.Parse(attributeValue.N);
        if (attributeValue.BOOL != null) return attributeValue.BOOL;
        return null;
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