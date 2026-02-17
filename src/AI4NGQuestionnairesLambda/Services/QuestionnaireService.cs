using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGQuestionnairesLambda.Interfaces;
using AI4NG.ExperimentManagement.Contracts.Questionnaires;

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

    // === Utility ===
    private static string NowIso() => DateTime.UtcNow.ToString("O");

    // === READ ===
    public async Task<IEnumerable<QuestionnaireDto>> GetAllAsync()
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

        return response.Items.Select(item => new QuestionnaireDto
        {
            Id = item["PK"].S.Replace("QUESTIONNAIRE#", ""),
            Data = ConvertAttributeValueToQuestionnaireData(item["data"]),
            CreatedAt = DateTime.TryParse(item.GetValueOrDefault("createdAt")?.S, out var created)
                ? created : DateTime.MinValue,
            UpdatedAt = DateTime.TryParse(item.GetValueOrDefault("updatedAt")?.S, out var updated)
                ? updated : DateTime.MinValue
        });
    }

    public async Task<QuestionnaireDto?> GetByIdAsync(string id)
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

        if (!response.IsItemSet || !response.Item.ContainsKey("data"))
            return null;

        var isDeleted = response.Item.GetValueOrDefault("syncMetadata")?.M?.GetValueOrDefault("isDeleted")?.BOOL ?? false;
        if (isDeleted) return null;

        return new QuestionnaireDto
        {
            Id = id,
            Data = ConvertAttributeValueToQuestionnaireData(response.Item["data"]),
            CreatedAt = DateTime.TryParse(response.Item.GetValueOrDefault("createdAt")?.S, out var created)
                ? created : DateTime.MinValue,
            UpdatedAt = DateTime.TryParse(response.Item.GetValueOrDefault("updatedAt")?.S, out var updated)
                ? updated : DateTime.MinValue
        };
    }

    public async Task<IEnumerable<QuestionnaireDto>> GetByIdsAsync(List<string> ids)
    {
        if (ids == null || !ids.Any())
            return new List<QuestionnaireDto>();

        var keys = ids.Select(id => new Dictionary<string, AttributeValue>
        {
            ["PK"] = new($"QUESTIONNAIRE#{id}"),
            ["SK"] = new("CONFIG")
        }).ToList();

        var request = new BatchGetItemRequest
        {
            RequestItems = new Dictionary<string, KeysAndAttributes>
            {
                [_tableName] = new KeysAndAttributes
                {
                    Keys = keys
                }
            }
        };

        var response = await _dynamoClient.BatchGetItemAsync(request);
        var items = response.Responses[_tableName];

        return items.Select(item => new QuestionnaireDto
        {
            Id = item["PK"].S.Replace("QUESTIONNAIRE#", ""),
            Data = ConvertAttributeValueToQuestionnaireData(item["data"]),
            CreatedAt = DateTime.TryParse(item.GetValueOrDefault("createdAt")?.S, out var created)
                ? created : DateTime.MinValue,
            UpdatedAt = DateTime.TryParse(item.GetValueOrDefault("updatedAt")?.S, out var updated)
                ? updated : DateTime.MinValue
        });
    }

    // === CREATE ===
    public async Task<string> CreateAsync(string id, QuestionnaireDataDto data, string username)
    {
        id = (id ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Questionnaire ID cannot be empty");

        ValidateQuestionnaireData(data);

        var timestamp = NowIso();

        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new($"QUESTIONNAIRE#{id}"),
            ["SK"] = new("CONFIG"),
            ["GSI3PK"] = new("QUESTIONNAIRE"),
            ["GSI3SK"] = new(timestamp),
            ["type"] = new("Questionnaire"),
            ["data"] = ConvertQuestionnaireDataToAttributeValue(data),
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
        };

        try
        {
            await _dynamoClient.PutItemAsync(new PutItemRequest
            {
                TableName = _tableName,
                Item = item,
                ConditionExpression = "attribute_not_exists(PK) AND attribute_not_exists(SK)"
            });
        }
        catch (ConditionalCheckFailedException)
        {
            throw new InvalidOperationException($"Questionnaire with ID '{id}' already exists");
        }

        return id;
    }

    // === UPDATE ===
    public async Task UpdateAsync(string id, QuestionnaireDataDto data, string username)
    {
        id = (id ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Questionnaire ID cannot be empty");

        ValidateQuestionnaireData(data);

        var timestamp = NowIso();

        try
        {
            await _dynamoClient.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = $"QUESTIONNAIRE#{id}" },
                    ["SK"] = new AttributeValue { S = "CONFIG" }
                },

                // ✅ Only update if it exists AND is not soft-deleted
                ConditionExpression =
                    "attribute_exists(PK) AND attribute_exists(SK) AND " +
                    "(attribute_not_exists(syncMetadata.isDeleted) OR syncMetadata.isDeleted = :notDeleted)",

                UpdateExpression = @"
                SET #data = :data,
                    updatedBy = :user,
                    updatedAt = :timestamp,
                    GSI3SK = :timestamp,
                    syncMetadata.#version = if_not_exists(syncMetadata.#version, :zero) + :inc,
                    syncMetadata.lastModified = :timestamp",

                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    ["#data"] = "data",
                    ["#version"] = "version"
                },

                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":data"] = ConvertQuestionnaireDataToAttributeValue(data),
                    [":user"] = new AttributeValue { S = username },
                    [":timestamp"] = new AttributeValue { S = timestamp },
                    [":inc"] = new AttributeValue { N = "1" },
                    [":zero"] = new AttributeValue { N = "0" },
                    [":notDeleted"] = new AttributeValue { BOOL = false }
                }
            });
        }
        catch (ConditionalCheckFailedException)
        {
            // treat missing OR deleted as 404
            throw new KeyNotFoundException($"Questionnaire '{id}' not found.");
        }
    }


    // === DELETE (soft) ===
    public async Task DeleteAsync(string id, string username)
    {
        var timestamp = NowIso();

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

    // === BATCH CREATE ===
    public async Task<BatchResult> CreateBatchAsync(List<CreateQuestionnaireRequest> requests, string username)
    {
        if (requests == null || !requests.Any())
            throw new ArgumentException("No questionnaires provided for batch import.");

        async Task<BatchItemResult> ProcessOneAsync(CreateQuestionnaireRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Id))
                    throw new ArgumentException("Questionnaire ID cannot be empty.");

                if (request.Data == null)
                    throw new ArgumentException("Questionnaire data cannot be null.");

                ValidateQuestions(request.Data.Questions);
                await CreateAsync(request.Id, request.Data, username);

                return new BatchItemResult(request.Id, "success");
            }
            catch (Exception ex)
            {
                var message = ex switch
                {
                    InvalidOperationException when ex.Message.Contains("already exists") => $"Questionnaire with ID '{request.Id}' already exists.",
                    ArgumentException => $"Invalid questionnaire '{request.Id}': {ex.Message}",
                    _ => $"Unexpected error creating '{request.Id}': {ex.Message}"
                };

                return new BatchItemResult(request.Id, "error", message);
            }
        }

        var results = await Task.WhenAll(requests.Select(ProcessOneAsync));
        var successes = results.Count(r => r.Status == "success");
        var failures = results.Length - successes;

        return new BatchResult(
            new BatchSummary(results.Length, successes, failures),
            results.ToList()
        );
    }

    // === VALIDATION ===

    private void ValidateQuestionnaireData(QuestionnaireDataDto data)
    {
        if (data == null)
            throw new ArgumentException("Questionnaire data cannot be null");

        if (string.IsNullOrWhiteSpace(data.Name))
            throw new ArgumentException("Questionnaire name cannot be empty");

        ValidateQuestions(data.Questions ?? new List<QuestionDto>());
    }
    private void ValidateQuestions(List<QuestionDto> questions)
    {
        if (questions == null) return;

        var questionIds = new HashSet<string>();
        var validTypes = new[] { "text", "choice", "select", "scale", "number", "boolean" };

        foreach (var question in questions)
        {
            if (question == null)
                throw new ArgumentException("Question cannot be null");

            if (string.IsNullOrWhiteSpace(question.Id))
                throw new ArgumentException("Question ID cannot be empty");

            if (!questionIds.Add(question.Id.Trim()))
                throw new ArgumentException($"Question ID '{question.Id}' is not unique");

            if (string.IsNullOrWhiteSpace(question.Text))
                throw new ArgumentException($"Question '{question.Id}' text cannot be empty");

            if (string.IsNullOrWhiteSpace(question.Type) || !validTypes.Contains(question.Type))
                throw new ArgumentException($"Question '{question.Id}' has invalid type '{question.Type}'");

            var type = question.Type.Trim();

            if ((type.Equals("choice", StringComparison.OrdinalIgnoreCase) ||
                 type.Equals("select", StringComparison.OrdinalIgnoreCase)) &&
                (question.Options == null || question.Options.Count == 0 || question.Options.Any(string.IsNullOrWhiteSpace)))
            {
                throw new ArgumentException($"Question '{question.Id}' of type '{type}' must have non-empty options");
            }

            if (type.Equals("scale", StringComparison.OrdinalIgnoreCase))
            {
                if (question.Scale == null)
                    throw new ArgumentException($"Question '{question.Id}' of type 'scale' must include a scale object");

                if (question.Scale.Min >= question.Scale.Max)
                    throw new ArgumentException($"Question '{question.Id}' has invalid scale range (min must be < max)");
            }
        }
    }

    // === CONVERSIONS ===
    private AttributeValue ConvertQuestionnaireDataToAttributeValue(QuestionnaireDataDto data)
    {
        var map = new Dictionary<string, AttributeValue>
        {
            ["name"] = new(data.Name),
            ["description"] = new(data.Description),
            ["estimatedTime"] = new AttributeValue { N = data.EstimatedTime.ToString() },
            ["version"] = new(data.Version),
            ["questions"] = new AttributeValue
            {
                L = (data.Questions ?? new List<QuestionDto>())
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

                        if (q.Scale != null)
                        {
                            var scaleMap = new Dictionary<string, AttributeValue>
                            {
                                ["min"] = new AttributeValue { N = q.Scale.Min.ToString() },
                                ["max"] = new AttributeValue { N = q.Scale.Max.ToString() },
                            };

                            if (!string.IsNullOrWhiteSpace(q.Scale.MinLabel))
                                scaleMap["minLabel"] = new AttributeValue { S = q.Scale.MinLabel };

                            if (!string.IsNullOrWhiteSpace(q.Scale.MaxLabel))
                                scaleMap["maxLabel"] = new AttributeValue { S = q.Scale.MaxLabel };

                            qMap["scale"] = new AttributeValue { M = scaleMap };
                        }

                        return new AttributeValue { M = qMap };
                    })
                    .ToList()
            }
        };

        return new AttributeValue { M = map };
    }

    private QuestionnaireDataDto ConvertAttributeValueToQuestionnaireData(AttributeValue attributeValue)
    {
        var data = attributeValue.M;
        return new QuestionnaireDataDto
        {
            Name = data["name"].S,
            Description = data.GetValueOrDefault("description")?.S ?? "",
            EstimatedTime = int.Parse(data.GetValueOrDefault("estimatedTime")?.N ?? "0"),
            Version = data.GetValueOrDefault("version")?.S ?? "1.0",
            Questions = data.GetValueOrDefault("questions")?.L?.Select(q => new QuestionDto
            {
                Id = q.M["id"].S,
                Text = q.M["text"].S,
                Type = q.M["type"].S,
                Options = q.M.GetValueOrDefault("options")?.L?.Select(o => o.S).ToList() ?? new(),
                Required = q.M.GetValueOrDefault("required")?.BOOL ?? true,
                Scale = q.M.ContainsKey("scale") && q.M["scale"].M != null
                    ? new ScaleDto
                    {
                        Min = int.Parse(q.M["scale"].M.GetValueOrDefault("min")?.N ?? "0"),
                        Max = int.Parse(q.M["scale"].M.GetValueOrDefault("max")?.N ?? "0"),
                        MinLabel = q.M["scale"].M.GetValueOrDefault("minLabel")?.S,
                        MaxLabel = q.M["scale"].M.GetValueOrDefault("maxLabel")?.S
                    }
                    : null
            }).ToList() ?? new()
        };
    }
}
