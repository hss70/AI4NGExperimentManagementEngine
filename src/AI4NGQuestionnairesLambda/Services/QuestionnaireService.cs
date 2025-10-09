using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGQuestionnairesLambda.Interfaces;
using AI4NGQuestionnairesLambda.Models;
using System.Text.Json;

namespace AI4NGQuestionnairesLambda.Services;

public class QuestionnaireService : IQuestionnaireService
{
    private readonly IAmazonDynamoDB _dynamoClient;
    private readonly string _tableName;

    public QuestionnaireService(IAmazonDynamoDB dynamoClient, IConfiguration configuration)
    {
        _dynamoClient = dynamoClient;
        _tableName = configuration["QUESTIONNAIRES_TABLE"] ?? "";
    }

    public async Task<IEnumerable<Questionnaire>> GetAllAsync()
    {
        var response = await _dynamoClient.ScanAsync(new ScanRequest
        {
            TableName = _tableName,
            FilterExpression = "#type = :type",
            ExpressionAttributeNames = new Dictionary<string, string> { ["#type"] = "type" },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":type"] = new("Questionnaire") }
        });

        return response.Items.Select(item => new Questionnaire
        {
            Id = item["PK"].S.Replace("QUESTIONNAIRE#", ""),
            Data = new QuestionnaireData
            {
                Name = item["data"].M["name"].S,
                Description = item["data"].M.GetValueOrDefault("description")?.S ?? "",
                Version = item["data"].M.GetValueOrDefault("version")?.S ?? "1.0",
                EstimatedTime = int.Parse(item["data"].M.GetValueOrDefault("estimatedTime")?.N ?? "0")
            }
        });
    }

    public async Task<Questionnaire?> GetByIdAsync(string id)
    {
        var response = await _dynamoClient.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"QUESTIONNAIRE#{id}"),
                ["SK"] = new("CONFIG")
            }
        });

        if (!response.IsItemSet) return null;

        return new Questionnaire
        {
            Id = id,
            Data = ConvertAttributeValueToQuestionnaireData(response.Item["data"]),
            CreatedAt = DateTime.Parse(response.Item.GetValueOrDefault("createdAt")?.S ?? DateTime.UtcNow.ToString("O")),
            UpdatedAt = DateTime.Parse(response.Item.GetValueOrDefault("updatedAt")?.S ?? DateTime.UtcNow.ToString("O"))
        };
    }

    public async Task<string> CreateAsync(CreateQuestionnaireRequest request, string username)
    {
        var questionnaireId = request.Id;
        var timestamp = DateTime.UtcNow.ToString("O");

        await _dynamoClient.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"QUESTIONNAIRE#{questionnaireId}"),
                ["SK"] = new("CONFIG"),
                ["GSI3PK"] = new("QUESTIONNAIRE"),
                ["GSI3SK"] = new(timestamp),
                ["type"] = new("Questionnaire"),
                ["data"] = ConvertQuestionnaireDataToAttributeValue(request.Data),
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

        return questionnaireId;
    }

    public async Task UpdateAsync(string id, QuestionnaireData data, string username)
    {
        var timestamp = DateTime.UtcNow.ToString("O");

        await _dynamoClient.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"QUESTIONNAIRE#{id}"),
                ["SK"] = new("CONFIG")
            },
            UpdateExpression = "SET #data = :data, updatedAt = :timestamp, GSI3SK = :timestamp, syncMetadata.#version = syncMetadata.#version + :inc, syncMetadata.lastModified = :timestamp",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#data"] = "data",
                ["#version"] = "version"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":data"] = ConvertQuestionnaireDataToAttributeValue(data),
                [":timestamp"] = new(timestamp),
                [":inc"] = new AttributeValue { N = "1" }
            }
        });
    }

    public async Task DeleteAsync(string id)
    {
        await _dynamoClient.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"QUESTIONNAIRE#{id}"),
                ["SK"] = new("CONFIG")
            }
        });
    }

    public async Task<Dictionary<string, object>> CreateBatchAsync(List<CreateQuestionnaireRequest> requests, string username)
    {
        var results = new List<object>();
        var timestamp = DateTime.UtcNow.ToString("O");

        foreach (var request in requests)
        {
            try
            {
                await CreateAsync(request, username);
                results.Add(new { id = request.Id, status = "success" });
            }
            catch (Exception ex)
            {
                results.Add(new { id = request.Id, status = "error", error = ex.Message });
            }
        }

        return new Dictionary<string, object>
        {
            ["message"] = $"Processed {requests.Count} questionnaires",
            ["results"] = results
        };
    }

    private AttributeValue ConvertQuestionnaireDataToAttributeValue(QuestionnaireData data)
    {
        return new AttributeValue
        {
            M = new Dictionary<string, AttributeValue>
            {
                ["name"] = new(data.Name),
                ["description"] = new(data.Description),
                ["estimatedTime"] = new AttributeValue { N = data.EstimatedTime.ToString() },
                ["version"] = new(data.Version),
                ["questions"] = new AttributeValue
                {
                    L = data.Questions.Select(q => new AttributeValue
                    {
                        M = new Dictionary<string, AttributeValue>
                        {
                            ["id"] = new(q.Id),
                            ["text"] = new(q.Text),
                            ["type"] = new(q.Type),
                            ["options"] = new AttributeValue { L = q.Options.Select(o => new AttributeValue(o)).ToList() },
                            ["required"] = new AttributeValue { BOOL = q.Required }
                        }
                    }).ToList()
                }
            }
        };
    }

    private QuestionnaireData ConvertAttributeValueToQuestionnaireData(AttributeValue attributeValue)
    {
        var data = attributeValue.M;
        return new QuestionnaireData
        {
            Name = data["name"].S,
            Description = data.GetValueOrDefault("description")?.S ?? "",
            EstimatedTime = int.Parse(data.GetValueOrDefault("estimatedTime")?.N ?? "0"),
            Version = data.GetValueOrDefault("version")?.S ?? "1.0",
            Questions = data.GetValueOrDefault("questions")?.L?.Select(q => new Question
            {
                Id = q.M["id"].S,
                Text = q.M["text"].S,
                Type = q.M["type"].S,
                Options = q.M.GetValueOrDefault("options")?.L?.Select(o => o.S).ToList() ?? new(),
                Required = q.M.GetValueOrDefault("required")?.BOOL ?? true
            }).ToList() ?? new()
        };
    }
}