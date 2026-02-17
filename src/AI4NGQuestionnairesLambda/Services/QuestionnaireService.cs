using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGQuestionnairesLambda.Interfaces;
using AI4NG.ExperimentManagement.Contracts.Questionnaires;
using System.Globalization;

namespace AI4NGQuestionnairesLambda.Services;

public class QuestionnaireService : IQuestionnaireService
{
    private readonly IAmazonDynamoDB _dynamoClient;
    private readonly string _tableName;

    public QuestionnaireService(IAmazonDynamoDB dynamoClient)
    {
        _dynamoClient = dynamoClient;
        _tableName = Environment.GetEnvironmentVariable("QUESTIONNAIRES_TABLE") ?? "";
        if (string.IsNullOrWhiteSpace(_tableName))
            throw new InvalidOperationException("Missing env var QUESTIONNAIRES_TABLE");
    }

    // === Utility ===

    // === Utility ===

    private static DateTimeOffset NowUtc() => DateTimeOffset.UtcNow;

    private static string ToIso(DateTimeOffset dto) =>
        dto.ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseIsoOrMin(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return DateTimeOffset.MinValue;

        return DateTimeOffset.TryParseExact(
            s,
            "O",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var dto)
            ? dto
            : DateTimeOffset.MinValue;
    }

    private static DateTime ParseIsoUtcDateTimeOrMin(string? s) =>
        ParseIsoOrMin(s).UtcDateTime;

    // === READ ===
    public async Task<IEnumerable<QuestionnaireDto>> GetAllAsync(CancellationToken ct = default)
    {
        var results = new List<Dictionary<string, AttributeValue>>();
        Dictionary<string, AttributeValue>? lastKey = null;

        do
        {
            var response = await _dynamoClient.QueryAsync(new QueryRequest
            {
                TableName = _tableName,
                IndexName = "GSI3",
                KeyConditionExpression = "GSI3PK = :pk",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":pk"] = new AttributeValue { S = "QUESTIONNAIRE" },
                    [":notDeleted"] = new AttributeValue { BOOL = false }
                },

                // Filter non-deleted (Query still needs filter because isDeleted isn't in keys)
                FilterExpression = "attribute_not_exists(syncMetadata.isDeleted) OR syncMetadata.isDeleted = :notDeleted",

                ExclusiveStartKey = lastKey
            }, ct);

            results.AddRange(response.Items);
            lastKey = response.LastEvaluatedKey;
        }
        while (lastKey != null && lastKey.Count > 0);

        return results.Select(MapToDto).ToList();
    }

    private QuestionnaireDto MapToDto(Dictionary<string, AttributeValue> item)
    {
        return new QuestionnaireDto
        {
            Id = item["PK"].S.Replace("QUESTIONNAIRE#", ""),
            Data = ConvertAttributeValueToQuestionnaireData(item["data"]),
            CreatedAt = ParseIsoUtcDateTimeOrMin(item.GetValueOrDefault("createdAt")?.S),
            UpdatedAt = ParseIsoUtcDateTimeOrMin(item.GetValueOrDefault("updatedAt")?.S)
        };
    }

    public async Task<QuestionnaireDto?> GetByIdAsync(string id, CancellationToken ct = default)
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
        }, ct);

        if (!response.IsItemSet || !response.Item.ContainsKey("data"))
            return null;

        var isDeleted = response.Item.GetValueOrDefault("syncMetadata")?.M?.GetValueOrDefault("isDeleted")?.BOOL ?? false;
        if (isDeleted) return null;

        return new QuestionnaireDto
        {
            Id = id,
            Data = ConvertAttributeValueToQuestionnaireData(response.Item["data"]),
            CreatedAt = ParseIsoUtcDateTimeOrMin(response.Item.GetValueOrDefault("createdAt")?.S),
            UpdatedAt = ParseIsoUtcDateTimeOrMin(response.Item.GetValueOrDefault("updatedAt")?.S)
        };
    }

    public async Task<IEnumerable<QuestionnaireDto>> GetByIdsAsync(List<string> ids, CancellationToken ct = default)
    {
        if (ids == null || ids.Count == 0)
            return Array.Empty<QuestionnaireDto>();

        // Optional: normalise + de-dupe here to avoid wasted capacity
        var uniqueIds = ids
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Select(i => i.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (uniqueIds.Count == 0)
            return Array.Empty<QuestionnaireDto>();

        const int maxAllowed = 1000;

        if (uniqueIds.Count > maxAllowed)
            throw new ArgumentException($"Maximum {maxAllowed} questionnaire IDs allowed per request.");


        // DynamoDB BatchGet has a max of 100 keys per request.
        const int batchSize = 100;
        var results = new List<QuestionnaireDto>();

        for (int i = 0; i < uniqueIds.Count; i += batchSize)
        {
            var chunk = uniqueIds.Skip(i).Take(batchSize).ToList();

            var keys = chunk.Select(qid => new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = $"QUESTIONNAIRE#{qid}" },
                ["SK"] = new AttributeValue { S = "CONFIG" }
            }).ToList();

            var requestItems = new Dictionary<string, KeysAndAttributes>
            {
                [_tableName] = new KeysAndAttributes { Keys = keys }
            };

            // Retry loop for UnprocessedKeys
            const int maxRetries = 6;
            var attempt = 0;

            do
            {
                var response = await _dynamoClient.BatchGetItemAsync(new BatchGetItemRequest
                {
                    RequestItems = requestItems
                }, ct);

                if (response.Responses.TryGetValue(_tableName, out var items) && items != null)
                {
                    foreach (var item in items)
                    {
                        // Skip soft-deleted
                        var isDeleted = item.GetValueOrDefault("syncMetadata")?.M?
                            .GetValueOrDefault("isDeleted")?.BOOL ?? false;

                        if (isDeleted) continue;

                        results.Add(MapToDto(item));
                    }
                }

                requestItems = response.UnprocessedKeys ?? new Dictionary<string, KeysAndAttributes>();

                if (requestItems.Count > 0)
                {
                    attempt++;

                    if (attempt > maxRetries)
                        throw new TimeoutException("BatchGetItem exceeded retry limit due to unprocessed keys.");

                    // Exponential backoff with jitter
                    var baseDelayMs = (int)(50 * Math.Pow(2, attempt)); // 100, 200, 400, ...
                    var jitterMs = Random.Shared.Next(0, 75);
                    await Task.Delay(baseDelayMs + jitterMs, ct);
                }
            }
            while (requestItems.Count > 0);
        }

        return results;
    }

    // === CREATE ===
    public async Task<string> CreateAsync(string id, QuestionnaireDataDto data, string username, CancellationToken ct = default)
    {
        id = (id ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Questionnaire ID cannot be empty");

        data = Normalise(data);
        ValidateQuestionnaireData(data);

        var timestamp = ToIso(NowUtc());

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
            }, ct);
        }
        catch (ConditionalCheckFailedException)
        {
            throw new InvalidOperationException($"Questionnaire with ID '{id}' already exists");
        }

        return id;
    }

    // === UPDATE ===
    public async Task UpdateAsync(string id, QuestionnaireDataDto data, string username, CancellationToken ct = default)
    {
        id = (id ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Questionnaire ID cannot be empty");

        data = Normalise(data);
        ValidateQuestionnaireData(data);

        var timestamp = ToIso(NowUtc());

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

                //Only update if it exists AND is not soft-deleted
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
            }, ct);
        }
        catch (ConditionalCheckFailedException)
        {
            // treat missing OR deleted as 404
            throw new KeyNotFoundException($"Questionnaire '{id}' not found.");
        }
    }


    // === DELETE (soft) ===
    public async Task DeleteAsync(string id, string username, CancellationToken ct = default)
    {
        id = (id ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Questionnaire ID cannot be empty");

        var timestamp = ToIso(NowUtc());

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

                ConditionExpression = "attribute_exists(PK) AND attribute_exists(SK)",

                UpdateExpression = @"
                SET deletedBy = :deletedBy,
                    deletedAt = :timestamp,
                    updatedBy = :deletedBy,
                    updatedAt = :timestamp,
                    GSI3SK = :timestamp,
                    syncMetadata.isDeleted = :deleted,
                    syncMetadata.lastModified = :timestamp,
                    syncMetadata.#version = if_not_exists(syncMetadata.#version, :zero) + :inc",

                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    ["#version"] = "version"
                },

                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":deletedBy"] = new AttributeValue { S = username },
                    [":timestamp"] = new AttributeValue { S = timestamp },
                    [":deleted"] = new AttributeValue { BOOL = true },
                    [":inc"] = new AttributeValue { N = "1" },
                    [":zero"] = new AttributeValue { N = "0" }
                }
            }, ct);
        }
        catch (ConditionalCheckFailedException)
        {
            // if missing, treat as already deleted
            return;
        }
    }

    // === BATCH CREATE ===
    public async Task<BatchResult> CreateBatchAsync(List<CreateQuestionnaireRequest> requests, string username, CancellationToken ct = default)
    {
        if (requests == null || requests.Count == 0)
            throw new ArgumentException("No questionnaires provided for batch import.");

        const int maxBatchSize = 500;
        if (requests.Count > maxBatchSize)
            throw new ArgumentException($"Batch import limit exceeded (max {maxBatchSize}).");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var duplicates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var r in requests)
        {
            var id = (r?.Id ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(id) && !seen.Add(id))
                duplicates.Add(id);
        }

        const int maxConcurrency = 8;
        using var semaphore = new SemaphoreSlim(maxConcurrency);

        async Task<BatchItemResult> ProcessOneAsync(CreateQuestionnaireRequest request)
        {
            await semaphore.WaitAsync(ct);
            try
            {
                if (request == null)
                    throw new ArgumentException("Request item cannot be null.");

                var id = (request.Id ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(id))
                    throw new ArgumentException("Questionnaire ID cannot be empty.");

                if (duplicates.Contains(id))
                    return new BatchItemResult(id, "error", $"Duplicate questionnaire id '{id}' in batch.");

                if (request.Data == null)
                    throw new ArgumentException("Questionnaire data cannot be null.");

                await CreateAsync(id, request.Data, username, ct);
                return new BatchItemResult(id, "success");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            {
                var id = (request?.Id ?? string.Empty).Trim();
                return new BatchItemResult(id, "error", ex.Message);
            }
            catch (ArgumentException ex)
            {
                var id = (request?.Id ?? string.Empty).Trim();
                return new BatchItemResult(id, "error", $"Invalid questionnaire '{id}': {ex.Message}");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var id = (request?.Id ?? string.Empty).Trim();
                return new BatchItemResult(id, "error", $"Unexpected error creating '{id}': {ex.Message}");
            }
            finally
            {
                semaphore.Release();
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

    private static QuestionnaireDataDto Normalise(QuestionnaireDataDto data)
    {
        data.Name = (data.Name ?? string.Empty).Trim();
        data.Description = (data.Description ?? string.Empty).Trim();
        data.Version = string.IsNullOrWhiteSpace(data.Version) ? "1.0" : data.Version.Trim();

        if (data.Questions != null)
        {
            foreach (var q in data.Questions)
            {
                if (q == null) continue;

                q.Id = (q.Id ?? string.Empty).Trim();
                q.Text = (q.Text ?? string.Empty).Trim();
                q.Type = (q.Type ?? string.Empty).Trim();

                q.Options ??= new List<string>();
                for (int i = 0; i < q.Options.Count; i++)
                {
                    q.Options[i] = (q.Options[i] ?? string.Empty).Trim();
                }

                if (q.Scale != null)
                {
                    q.Scale.MinLabel = q.Scale.MinLabel?.Trim();
                    q.Scale.MaxLabel = q.Scale.MaxLabel?.Trim();
                }
            }
        }

        return data;
    }

    private void ValidateQuestionnaireData(QuestionnaireDataDto data)
    {
        if (data == null)
            throw new ArgumentException("Questionnaire data cannot be null");

        if (string.IsNullOrWhiteSpace(data.Name))
            throw new ArgumentException("Questionnaire name cannot be empty");

        if (data.Description != null && data.Description.Length > 2000)
            throw new ArgumentException("Questionnaire description is too long (max 2000 chars)");

        if (data.EstimatedTime < 0)
            throw new ArgumentException("EstimatedTime must be >= 0");

        if (string.IsNullOrWhiteSpace(data.Version))
            throw new ArgumentException("Questionnaire version cannot be empty");

        if (data.Questions == null || data.Questions.Count == 0)
            throw new ArgumentException("Questionnaire must contain at least one question");

        ValidateQuestions(data.Questions);
    }

    private void ValidateQuestions(List<QuestionDto>? questions)
    {
        if (questions == null) return;

        // Case-insensitive valid types
        var validTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "text", "choice", "select", "scale", "number", "boolean"
        };

        // Case-insensitive IDs (pick OrdinalIgnoreCase unless you want IDs case-sensitive)
        var questionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var question in questions)
        {
            if (question == null)
                throw new ArgumentException("Question cannot be null");

            var id = (question.Id ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Question ID cannot be empty");

            if (!questionIds.Add(id))
                throw new ArgumentException($"Question ID '{id}' is not unique");

            var text = (question.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException($"Question '{id}' text cannot be empty");

            var type = (question.Type ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(type) || !validTypes.Contains(type))
                throw new ArgumentException($"Question '{id}' has invalid type '{type}'");

            // choice/select need options
            if (type.Equals("choice", StringComparison.OrdinalIgnoreCase) ||
                type.Equals("select", StringComparison.OrdinalIgnoreCase))
            {
                if (question.Options == null)
                    throw new ArgumentException($"Question '{id}' of type '{type}' must have options");

                var cleaned = question.Options
                    .Select(o => (o ?? string.Empty).Trim())
                    .Where(o => !string.IsNullOrWhiteSpace(o))
                    .ToList();

                if (cleaned.Count < 2)
                    throw new ArgumentException($"Question '{id}' of type '{type}' must have at least 2 non-empty options");

                if (cleaned.Distinct(StringComparer.OrdinalIgnoreCase).Count() != cleaned.Count)
                    throw new ArgumentException($"Question '{id}' of type '{type}' has duplicate options");
            }

            if (type.Equals("scale", StringComparison.OrdinalIgnoreCase))
            {
                if (question.Scale == null)
                    throw new ArgumentException($"Question '{id}' of type 'scale' must include a scale object");

                if (question.Scale.Min >= question.Scale.Max)
                    throw new ArgumentException($"Question '{id}' has invalid scale range (min must be < max)");

                // keep labels sane (avoid massive payloads)
                if (question.Scale.MinLabel != null && question.Scale.MinLabel.Length > 100)
                    throw new ArgumentException($"Question '{id}' scale minLabel is too long (max 100 chars)");

                if (question.Scale.MaxLabel != null && question.Scale.MaxLabel.Length > 100)
                    throw new ArgumentException($"Question '{id}' scale maxLabel is too long (max 100 chars)");
            }

            if (!type.Equals("scale", StringComparison.OrdinalIgnoreCase) && question.Scale != null)
                throw new ArgumentException($"Question '{id}' of type '{type}' must not include a scale object");

            if (!(type.Equals("choice", StringComparison.OrdinalIgnoreCase) || type.Equals("select", StringComparison.OrdinalIgnoreCase))
                && question.Options != null && question.Options.Count > 0)
                throw new ArgumentException($"Question '{id}' of type '{type}' must not include options");
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
