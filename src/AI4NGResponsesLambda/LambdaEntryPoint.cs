using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace AI4NGResponsesLambda;

public class LambdaEntryPoint
{
    private readonly AmazonDynamoDBClient _dynamoClient;
    private readonly string _responsesTable;

    public LambdaEntryPoint()
    {
        _dynamoClient = new AmazonDynamoDBClient();
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
                "GET" => await GetResponses(request.PathParameters?["experimentId"] ?? "", username),
                "POST" => await SubmitResponse(request.Body, username),
                _ => Error(405, "Method not allowed")
            };
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error: {ex.Message}");
            return Error(500, ex.Message);
        }
    }

    private async Task<APIGatewayProxyResponse> SubmitResponse(string body, string username)
    {
        var response = JsonSerializer.Deserialize<JsonElement>(body);
        var experimentId = response.GetProperty("experimentId").GetString();
        var questionnaireId = response.GetProperty("questionnaireId").GetString();
        var sessionId = response.GetProperty("sessionId").GetString();
        var taskId = response.GetProperty("taskId").GetString();
        var answers = response.GetProperty("answers").EnumerateArray();

        foreach (var answer in answers)
        {
            var questionId = answer.GetProperty("questionId").GetString();
            var answerValue = answer.GetProperty("answerValue").GetString();
            var questionText = answer.GetProperty("questionText").GetString();
            var timestamp = DateTime.UtcNow.ToString("O");

            await _dynamoClient.PutItemAsync(new PutItemRequest
            {
                TableName = _responsesTable,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new($"ANSWER#{experimentId}#{username}#{questionnaireId}"),
                    ["SK"] = new($"{timestamp}#{questionId}"),
                    ["GSI1PK"] = new($"SESSION#{experimentId}#{sessionId}#TASK#{taskId}"),
                    ["GSI1SK"] = new(questionId),
                    ["GSI2PK"] = new($"USER#{username}"),
                    ["GSI2SK"] = new(timestamp),
                    ["experimentId"] = new(experimentId),
                    ["userId"] = new(username),
                    ["sessionId"] = new(sessionId),
                    ["taskId"] = new(taskId),
                    ["questionnaireId"] = new(questionnaireId),
                    ["questionId"] = new(questionId),
                    ["questionText"] = new(questionText),
                    ["answerValue"] = new(answerValue),
                    ["timestamp"] = new(timestamp)
                }
            });
        }

        return Success(new { message = "Responses submitted successfully" });
    }

    private async Task<APIGatewayProxyResponse> GetResponses(string experimentId, string username)
    {
        var response = await _dynamoClient.QueryAsync(new QueryRequest
        {
            TableName = _responsesTable,
            IndexName = "GSI2",
            KeyConditionExpression = "GSI2PK = :pk",
            FilterExpression = "experimentId = :expId",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"USER#{username}"),
                [":expId"] = new(experimentId)
            }
        });

        var answers = response.Items.Select(item => new
        {
            questionnaireId = item["questionnaireId"].S,
            questionId = item["questionId"].S,
            questionText = item["questionText"].S,
            answerValue = item["answerValue"].S,
            timestamp = item["timestamp"].S,
            sessionId = item["sessionId"].S,
            taskId = item["taskId"].S
        });

        return Success(answers);
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