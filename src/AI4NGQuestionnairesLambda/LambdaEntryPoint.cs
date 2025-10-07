using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace AI4NGQuestionnairesLambda;

public class LambdaEntryPoint
{
    private readonly AmazonDynamoDBClient _dynamoClient;
    private readonly string _questionnairesTable;

    public LambdaEntryPoint()
    {
        _dynamoClient = new AmazonDynamoDBClient();
        _questionnairesTable = Environment.GetEnvironmentVariable("QUESTIONNAIRES_TABLE") ?? "";
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
                "GET" when request.PathParameters?.ContainsKey("questionnaireId") == true => 
                    await GetQuestionnaire(request.PathParameters["questionnaireId"]),
                "GET" => await GetQuestionnaires(),
                "POST" => await CreateQuestionnaire(request.Body, username),
                "PUT" => await UpdateQuestionnaire(request.PathParameters?["questionnaireId"] ?? "", request.Body, username),
                _ => Error(405, "Method not allowed")
            };
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error: {ex.Message}");
            return Error(500, ex.Message);
        }
    }

    private async Task<APIGatewayProxyResponse> GetQuestionnaires()
    {
        var response = await _dynamoClient.ScanAsync(new ScanRequest
        {
            TableName = _questionnairesTable,
            FilterExpression = "#type = :type",
            ExpressionAttributeNames = new Dictionary<string, string> { ["#type"] = "type" },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":type"] = new("Questionnaire") }
        });

        var questionnaires = response.Items.Select(item => new
        {
            id = item["PK"].S.Replace("QUESTIONNAIRE#", ""),
            name = item["data"].M["name"].S,
            description = item["data"].M.GetValueOrDefault("description")?.S ?? "",
            version = item["data"].M.GetValueOrDefault("version")?.S ?? "1.0",
            estimatedTime = item["data"].M.GetValueOrDefault("estimatedTime")?.N ?? "0"
        });

        return Success(questionnaires);
    }

    private async Task<APIGatewayProxyResponse> GetQuestionnaire(string questionnaireId)
    {
        var response = await _dynamoClient.GetItemAsync(new GetItemRequest
        {
            TableName = _questionnairesTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"QUESTIONNAIRE#{questionnaireId}"),
                ["SK"] = new("CONFIG")
            }
        });

        if (!response.IsItemSet)
            return Error(404, "Questionnaire not found");

        return Success(new
        {
            id = questionnaireId,
            data = ConvertAttributeValueToObject(response.Item["data"]),
            createdAt = response.Item.GetValueOrDefault("createdAt")?.S,
            updatedAt = response.Item.GetValueOrDefault("updatedAt")?.S
        });
    }

    private async Task<APIGatewayProxyResponse> CreateQuestionnaire(string body, string username)
    {
        var questionnaire = JsonSerializer.Deserialize<JsonElement>(body);
        var questionnaireId = questionnaire.GetProperty("id").GetString() ?? Guid.NewGuid().ToString();
        var timestamp = DateTime.UtcNow.ToString("O");

        await _dynamoClient.PutItemAsync(new PutItemRequest
        {
            TableName = _questionnairesTable,
            Item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"QUESTIONNAIRE#{questionnaireId}"),
                ["SK"] = new("CONFIG"),
                ["type"] = new("Questionnaire"),
                ["data"] = new AttributeValue { M = JsonToAttributeValue(questionnaire.GetProperty("data")) },
                ["createdAt"] = new(timestamp),
                ["updatedAt"] = new(timestamp),
                ["syncMetadata"] = new AttributeValue { M = new Dictionary<string, AttributeValue>
                {
                    ["version"] = new AttributeValue { N = "1" },
                    ["lastModified"] = new(timestamp),
                    ["isDeleted"] = new AttributeValue { BOOL = false }
                }}
            }
        });

        return Success(new { id = questionnaireId });
    }

    private async Task<APIGatewayProxyResponse> UpdateQuestionnaire(string questionnaireId, string body, string username)
    {
        var questionnaire = JsonSerializer.Deserialize<JsonElement>(body);
        var timestamp = DateTime.UtcNow.ToString("O");

        await _dynamoClient.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _questionnairesTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"QUESTIONNAIRE#{questionnaireId}"),
                ["SK"] = new("CONFIG")
            },
            UpdateExpression = "SET #data = :data, updatedAt = :timestamp, syncMetadata.#version = syncMetadata.#version + :inc, syncMetadata.lastModified = :timestamp",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#data"] = "data",
                ["#version"] = "version"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":data"] = new AttributeValue { M = JsonToAttributeValue(questionnaire.GetProperty("data")) },
                [":timestamp"] = new(timestamp),
                [":inc"] = new AttributeValue { N = "1" }
            }
        });

        return Success(new { message = "Questionnaire updated successfully" });
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
                JsonValueKind.Array => new AttributeValue { L = prop.Value.EnumerateArray().Select(v => new AttributeValue(v.ToString())).ToList() },
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
}