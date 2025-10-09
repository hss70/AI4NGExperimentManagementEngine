using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGExperimentsLambda.Interfaces;
using AI4NGExperimentsLambda.Models;
using System.Text.Json;

namespace AI4NGExperimentsLambda.Services;

public class ExperimentService : IExperimentService
{
    private readonly IAmazonDynamoDB _dynamoClient;
    private readonly string _experimentsTable;
    private readonly string _responsesTable;

    public ExperimentService(IAmazonDynamoDB dynamoClient)
    {
        _dynamoClient = dynamoClient;
        _experimentsTable = Environment.GetEnvironmentVariable("EXPERIMENTS_TABLE") ?? "";
        _responsesTable = Environment.GetEnvironmentVariable("RESPONSES_TABLE") ?? "";
    }

    public async Task<IEnumerable<object>> GetExperimentsAsync()
    {
        var response = await _dynamoClient.ScanAsync(new ScanRequest
        {
            TableName = _experimentsTable,
            FilterExpression = "#type = :type",
            ExpressionAttributeNames = new Dictionary<string, string> { ["#type"] = "type" },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":type"] = new("Experiment") }
        });

        return response.Items.Select(item => new
        {
            id = item["PK"].S.Replace("EXPERIMENT#", ""),
            name = item["data"].M["name"].S,
            description = item["data"].M.GetValueOrDefault("description")?.S ?? ""
        });
    }

    public async Task<object?> GetExperimentAsync(string experimentId)
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
            return null;

        var experiment = response.Items.First(i => i["SK"].S == "METADATA");
        var sessions = response.Items.Where(i => i["SK"].S.StartsWith("SESSION#")).ToList();

        return new
        {
            id = experimentId,
            data = ConvertAttributeValueToObject(experiment["data"]),
            questionnaireConfig = ConvertAttributeValueToObject(experiment["questionnaireConfig"]),
            sessions = sessions.Select(s => ConvertAttributeValueToObject(s["data"]))
        };
    }

    public async Task<IEnumerable<object>> GetMyExperimentsAsync(string username)
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

        return response.Items.Select(item => new
        {
            id = item["PK"].S.Replace("EXPERIMENT#", ""),
            name = item["data"].M["name"].S,
            description = item["data"].M.GetValueOrDefault("description")?.S ?? "",
            role = item["role"]?.S ?? "participant"
        });
    }

    public async Task<object> CreateExperimentAsync(Experiment experiment, string username)
    {
        var experimentId = Guid.NewGuid().ToString();

        await _dynamoClient.PutItemAsync(new PutItemRequest
        {
            TableName = _experimentsTable,
            Item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"EXPERIMENT#{experimentId}"),
                ["SK"] = new("METADATA"),
                ["type"] = new("Experiment"),
                ["data"] = new AttributeValue { M = JsonToAttributeValue(JsonSerializer.SerializeToElement(experiment.Data)) },
                ["questionnaireConfig"] = new AttributeValue { M = JsonToAttributeValue(JsonSerializer.SerializeToElement(experiment.QuestionnaireConfig)) },
                ["createdBy"] = new(username),
                ["createdAt"] = new(DateTime.UtcNow.ToString("O"))
            }
        });

        return new { id = experimentId };
    }

    public async Task UpdateExperimentAsync(string experimentId, ExperimentData data, string username)
    {
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
                [":data"] = new AttributeValue { M = JsonToAttributeValue(JsonSerializer.SerializeToElement(data)) },
                [":user"] = new(username),
                [":timestamp"] = new(DateTime.UtcNow.ToString("O"))
            }
        });
    }

    public async Task DeleteExperimentAsync(string experimentId, string username)
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
    }

    public async Task SyncExperimentAsync(string experimentId, SyncRequest syncData, string username)
    {
        foreach (var session in syncData.Sessions)
        {
            await _dynamoClient.PutItemAsync(new PutItemRequest
            {
                TableName = _experimentsTable,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new($"EXPERIMENT#{experimentId}"),
                    ["SK"] = new($"SESSION#{session.SessionId}"),
                    ["type"] = new("Session"),
                    ["data"] = new AttributeValue { M = JsonToAttributeValue(JsonSerializer.SerializeToElement(session)) },
                    ["GSI1PK"] = new($"EXPERIMENT#{experimentId}"),
                    ["GSI1SK"] = new($"SESSION#{session.SessionId}"),
                    ["updatedBy"] = new(username),
                    ["updatedAt"] = new(DateTime.UtcNow.ToString("O"))
                }
            });
        }
    }

    public async Task<IEnumerable<object>> GetExperimentMembersAsync(string experimentId)
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

        return response.Items.Select(item => new
        {
            userSub = item["SK"].S.Replace("MEMBER#", ""),
            role = item["role"]?.S ?? "participant",
            addedAt = item["addedAt"]?.S
        });
    }

    public async Task AddMemberAsync(string experimentId, string userSub, MemberRequest memberData, string username)
    {
        await _dynamoClient.PutItemAsync(new PutItemRequest
        {
            TableName = _experimentsTable,
            Item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"EXPERIMENT#{experimentId}"),
                ["SK"] = new($"MEMBER#{userSub}"),
                ["type"] = new("Member"),
                ["role"] = new(memberData.Role),
                ["addedBy"] = new(username),
                ["addedAt"] = new(DateTime.UtcNow.ToString("O"))
            }
        });
    }

    public async Task RemoveMemberAsync(string experimentId, string userSub, string username)
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
    }

    private static Dictionary<string, AttributeValue> JsonToAttributeValue(JsonElement element)
    {
        var result = new Dictionary<string, AttributeValue>();
        foreach (var property in element.EnumerateObject())
        {
            result[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.String => new AttributeValue(property.Value.GetString()),
                JsonValueKind.Number => new AttributeValue { N = property.Value.GetRawText() },
                JsonValueKind.True or JsonValueKind.False => new AttributeValue { BOOL = property.Value.GetBoolean() },
                JsonValueKind.Array => new AttributeValue { L = property.Value.EnumerateArray().Select(v => new AttributeValue(v.GetString())).ToList() },
                _ => new AttributeValue(property.Value.GetRawText())
            };
        }
        return result;
    }

    private static object ConvertAttributeValueToObject(AttributeValue attributeValue)
    {
        if (attributeValue.M != null)
        {
            var result = new Dictionary<string, object>();
            foreach (var kvp in attributeValue.M)
            {
                result[kvp.Key] = ConvertAttributeValueToObject(kvp.Value);
            }
            return result;
        }
        if (attributeValue.L != null)
            return attributeValue.L.Select(ConvertAttributeValueToObject).ToList();
        if (attributeValue.S != null)
            return attributeValue.S;
        if (attributeValue.N != null)
            return decimal.Parse(attributeValue.N);
        if (attributeValue.BOOL.HasValue)
            return attributeValue.BOOL.Value;
        return attributeValue.NULL ? null : attributeValue.S ?? "";
    }
}