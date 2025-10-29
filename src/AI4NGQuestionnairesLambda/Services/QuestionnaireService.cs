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

    public QuestionnaireService(IAmazonDynamoDB dynamoClient)
    {
        _dynamoClient = dynamoClient;
        _tableName = Environment.GetEnvironmentVariable("QUESTIONNAIRES_TABLE") ?? "";
    }

    public async Task<IEnumerable<Questionnaire>> GetAllAsync()
    {
        var response = await _dynamoClient.ScanAsync(new ScanRequest
        {
            TableName = _tableName,
            FilterExpression = "#type = :type AND (attribute_not_exists(syncMetadata.isDeleted) OR syncMetadata.isDeleted = :notDeleted)",
            ExpressionAttributeNames = new Dictionary<string, string> { ["#type"] = "type" },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":type"] = new("Questionnaire"),
                [":notDeleted"] = new AttributeValue { BOOL = false }
            }
        });

        return response.Items.Select(item => new Questionnaire
        {
            Id = item["PK"].S.Replace("QUESTIONNAIRE#", ""),
            Data = ConvertAttributeValueToQuestionnaireData(item["data"])
        });
    }

    public async Task<Questionnaire?> GetByIdAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

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

        if (!response.Item.ContainsKey("data"))
            return null;

        // Check if item is soft-deleted
        var isDeleted = response.Item.GetValueOrDefault("syncMetadata")?.M?.GetValueOrDefault("isDeleted")?.BOOL ?? false;
        if (isDeleted) return null;

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
        // Input validation
        if (request?.Data == null)
            throw new ArgumentException("Request data cannot be null");

        if (string.IsNullOrWhiteSpace(request.Data.Name))
            throw new ArgumentException("Questionnaire name cannot be empty");

        // Validate ID
        if (string.IsNullOrWhiteSpace(request.Id))
            throw new ArgumentException("Questionnaire ID cannot be empty");

        // Check for duplicate questionnaire ID (excluding soft-deleted items)
        var existingItem = await _dynamoClient.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"QUESTIONNAIRE#{request.Id}"),
                ["SK"] = new("CONFIG")
            }
        });

        if (existingItem.IsItemSet)
        {
            var isDeleted = existingItem.Item.GetValueOrDefault("syncMetadata")?.M?.GetValueOrDefault("isDeleted")?.BOOL ?? false;
            if (!isDeleted)
                throw new DuplicateItemException($"Questionnaire with ID '{request.Id}' already exists");

            // If soft-deleted, we can reuse the ID by overwriting
        }

        // Validate question structure
        if (request.Data?.Questions != null)
            ValidateQuestions(request.Data.Questions);

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
                ["data"] = ConvertQuestionnaireDataToAttributeValue(request.Data!),
                ["createdBy"] = new(username),
                ["createdAt"] = new(timestamp),
                ["updatedBy"] = new(username),
                ["updatedAt"] = new(timestamp),
                ["syncMetadata"] = new AttributeValue
                {
                    M = new Dictionary<string, AttributeValue>
                    {
                        ["version"] = new AttributeValue { N = "1" },
                        ["lastModified"] = new(timestamp),
                        ["isDeleted"] = new AttributeValue { BOOL = false }
                    }
                }
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
            UpdateExpression = "SET #data = :data, updatedBy = :user, updatedAt = :timestamp, GSI3SK = :timestamp, syncMetadata.#version = syncMetadata.#version + :inc, syncMetadata.lastModified = :timestamp",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#data"] = "data",
                ["#version"] = "version"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":data"] = ConvertQuestionnaireDataToAttributeValue(data),
                [":user"] = new(username),
                [":timestamp"] = new(timestamp),
                [":inc"] = new AttributeValue { N = "1" }
            }
        });
    }

    public async Task DeleteAsync(string id, string username)
    {
        var timestamp = DateTime.UtcNow.ToString("O");

        // Soft delete by updating syncMetadata
        await _dynamoClient.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"QUESTIONNAIRE#{id}"),
                ["SK"] = new("CONFIG")
            },
            UpdateExpression = "SET #deletedBy = :deletedBy, deletedAt = :timestamp, syncMetadata.isDeleted = :deleted, syncMetadata.lastModified = :timestamp",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#deletedBy"] = "deletedBy"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":deletedBy"] = new(username),
                [":timestamp"] = new(timestamp),
                [":deleted"] = new AttributeValue { BOOL = true }
            }
        });
    }

    public async Task<BatchResult> CreateBatchAsync(List<CreateQuestionnaireRequest> requests, string username)
    {
        if (requests == null || !requests.Any())
            throw new ArgumentException("No questionnaires provided for batch import.");

        var timestamp = DateTime.UtcNow.ToString("O");
        async Task<BatchItemResult> ProcessOneAsync(CreateQuestionnaireRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Id))
                    throw new ArgumentException("Questionnaire ID cannot be empty.");

                if (request.Data == null)
                    throw new ArgumentException("Questionnaire data cannot be null.");

                ValidateQuestions(request.Data.Questions);
                await CreateAsync(request, username);

                return new BatchItemResult(request.Id, "success");
            }
            catch (Exception ex)
            {
                var message = ex switch
                {
                    DuplicateItemException => $"Questionnaire with ID '{request.Id}' already exists.",
                    ArgumentException => $"Invalid questionnaire '{request.Id}': {ex.Message}",
                    _ => $"Unexpected error creating '{request.Id}': {ex.Message}"
                };

                return new BatchItemResult(request.Id, "error", message);
            }
        }

        // Run each create in parallel for speed & isolation
        var tasks = requests.Select(ProcessOneAsync);

        var results = await Task.WhenAll(tasks);
        var successes = results.Count(r => r.Status == "success");
        var failures = results.Length - successes;

        return new BatchResult(
            new BatchSummary(results.Length, successes, failures),
            results.ToList()
        );
    }

    private void ValidateQuestions(List<Question> questions)
    {
        if (questions == null) return;

        var questionIds = new HashSet<string>();
        var validTypes = new[] { "text", "choice", "select", "scale", "number", "boolean" };

        foreach (var question in questions)
        {
            if (question == null) continue;

            // Check question ID uniqueness
            if (!questionIds.Add(question.Id ?? ""))
                throw new ArgumentException($"Question ID '{question.Id}' is not unique");

            // Validate required fields
            if (string.IsNullOrWhiteSpace(question.Text))
                throw new ArgumentException($"Question '{question.Id}' text cannot be empty");

            // Validate question type
            if (string.IsNullOrWhiteSpace(question.Type) || !validTypes.Contains(question.Type))
                throw new ArgumentException($"Question '{question.Id}' has invalid type '{question.Type}'");

            // Validate options for select type
            if (question.Type == "select" && (question.Options == null || !question.Options.Any()))
                throw new ArgumentException($"Question '{question.Id}' of type 'select' must have options");
        }
    }

    private AttributeValue ConvertQuestionnaireDataToAttributeValue(QuestionnaireData data)
    {
        var map = new Dictionary<string, AttributeValue>
        {
            ["name"] = new(data.Name),
            ["description"] = new(data.Description),
            ["estimatedTime"] = new AttributeValue { N = data.EstimatedTime.ToString() },
            ["version"] = new(data.Version),
            ["questions"] = new AttributeValue
            {
                L = (data.Questions ?? new List<Question>())
                    .Select(q =>
                    {
                        var qMap = new Dictionary<string, AttributeValue>
                        {
                            ["id"] = new(q.Id),
                            ["text"] = new(q.Text),
                            ["type"] = new(q.Type),
                            ["options"] = new AttributeValue
                            {
                                L = (q.Options ?? new List<string>())
                                    .Select(o => new AttributeValue(o))
                                    .ToList()
                            },
                            ["required"] = new AttributeValue { BOOL = q.Required }
                        };

                        // Only include scale if it actually exists
                        if (q.Scale != null)
                        {
                            qMap["scale"] = new AttributeValue
                            {
                                M = new Dictionary<string, AttributeValue>
                                {
                                    ["min"] = new AttributeValue { N = q.Scale.Min.ToString() },
                                    ["max"] = new AttributeValue { N = q.Scale.Max.ToString() }
                                }
                            };
                        }

                        return new AttributeValue { M = qMap };
                    })
                    .ToList()
            }
        };

        return new AttributeValue { M = map };
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
                Required = q.M.GetValueOrDefault("required")?.BOOL ?? true,
                Scale = q.M.ContainsKey("scale") && q.M["scale"].M != null
                    ? new Scale
                    {
                        Min = int.Parse(q.M["scale"].M.GetValueOrDefault("min")?.N ?? "0"),
                        Max = int.Parse(q.M["scale"].M.GetValueOrDefault("max")?.N ?? "0")
                    }
                    : null
            }).ToList() ?? new()
        };
    }
}