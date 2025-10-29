using Xunit;
using Moq;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGQuestionnairesLambda.Services;
using AI4NGQuestionnairesLambda.Models;
using System.Text.Json;

namespace AI4NGQuestionnaires.Tests;

public class QuestionnaireServiceTests
{
    private readonly Mock<IAmazonDynamoDB> _mockDynamoClient;
    private readonly QuestionnaireService _service;

    public QuestionnaireServiceTests()
    {
        _mockDynamoClient = new Mock<IAmazonDynamoDB>();
        Environment.SetEnvironmentVariable("QUESTIONNAIRES_TABLE", "test-table");
        _service = new QuestionnaireService(_mockDynamoClient.Object);
    }

    // -------------------------------
    // Core CRUD Tests
    // -------------------------------

    [Fact]
    public async Task CreateAsync_ShouldReturnId_WhenValidRequest()
    {
        var request = new CreateQuestionnaireRequest
        {
            Id = "test-id",
            Data = new QuestionnaireData { Name = "Test Questionnaire" }
        };

        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse { Item = null });
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ReturnsAsync(new PutItemResponse());

        var result = await _service.CreateAsync(request, "testuser");

        Assert.Equal("test-id", result);
        _mockDynamoClient.Verify(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnQuestionnaires_WhenDataExists()
    {
        var scanResponse = new ScanResponse
        {
            Items = new List<Dictionary<string, AttributeValue>>
            {
                new()
                {
                    ["PK"] = new AttributeValue("QUESTIONNAIRE#test-id"),
                    ["data"] = new AttributeValue
                    {
                        M = new Dictionary<string, AttributeValue>
                        {
                            ["name"] = new AttributeValue("Test Questionnaire")
                        }
                    }
                }
            }
        };

        _mockDynamoClient.Setup(x => x.ScanAsync(It.IsAny<ScanRequest>(), default))
            .ReturnsAsync(scanResponse);

        var result = await _service.GetAllAsync();

        Assert.Single(result);
        _mockDynamoClient.Verify(x => x.ScanAsync(It.IsAny<ScanRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenNotFound()
    {
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse { Item = null });

        var result = await _service.GetByIdAsync("nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnQuestionnaire_WhenExists()
    {
        var getResponse = new GetItemResponse
        {
            Item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue("QUESTIONNAIRE#test-id"),
                ["data"] = new AttributeValue { M = new Dictionary<string, AttributeValue> { ["name"] = new AttributeValue("Test") } },
                ["createdBy"] = new AttributeValue("testuser"),
                ["createdAt"] = new AttributeValue("2023-11-01T09:00:00Z")
            }
        };

        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(getResponse);

        var result = await _service.GetByIdAsync("test-id");

        Assert.NotNull(result);
        Assert.Equal("Test", result.Data.Name);
    }

    [Fact]
    public async Task UpdateAsync_ShouldCallUpdateItem()
    {
        var data = new QuestionnaireData { Name = "Updated Questionnaire" };
        _mockDynamoClient.Setup(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .ReturnsAsync(new UpdateItemResponse());

        await _service.UpdateAsync("test-id", data, "testuser");

        _mockDynamoClient.Verify(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_ShouldCallUpdateItem()
    {
        _mockDynamoClient.Setup(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .ReturnsAsync(new UpdateItemResponse());

        await _service.DeleteAsync("test-id", "testuser");

        _mockDynamoClient.Verify(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default), Times.Once);
    }

    // -------------------------------
    // Serialization & Round-trip
    // -------------------------------

    [Fact]
    public async Task CreateAsync_ShouldSerializeAndDeserializeQuestionsAndScale()
    {
        var request = new CreateQuestionnaireRequest
        {
            Id = "PQ1",
            Data = new QuestionnaireData
            {
                Name = "Presence Questionnaire",
                Description = "Measures presence",
                EstimatedTime = 120,
                Version = "1.0",
                Questions = new List<Question>
                {
                    new()
                    {
                        Id = "1",
                        Text = "How much were you able to control events?",
                        Type = "scale",
                        Required = true,
                        Options = new(),
                        Scale = new Scale { Min = 1, Max = 7 }
                    }
                }
            }
        };

        Dictionary<string, AttributeValue>? savedItem = null;

        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse { Item = null });
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .Callback<PutItemRequest, CancellationToken>((req, _) => savedItem = req.Item)
            .ReturnsAsync(new PutItemResponse());

        await _service.CreateAsync(request, "testuser");

        Assert.NotNull(savedItem);
        var question = savedItem!["data"].M["questions"].L.First().M;
        Assert.True(question.ContainsKey("scale"));
        Assert.Equal("1", question["scale"].M["min"].N);
        Assert.Equal("7", question["scale"].M["max"].N);
    }

    [Fact]
    public async Task CreateAsync_ShouldRoundTripDataCorrectly_FromImportJson()
    {
        var requests = LoadBatchImportData();
        var request = requests.First(r => !string.IsNullOrWhiteSpace(r.Data.Name));

        Dictionary<string, AttributeValue>? savedItem = null;

        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse { Item = null });
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .Callback<PutItemRequest, CancellationToken>((req, _) => savedItem = req.Item)
            .ReturnsAsync(new PutItemResponse());

        await _service.CreateAsync(request, "testuser");

        Assert.NotNull(savedItem);

        var convertMethod = typeof(QuestionnaireService)
            .GetMethod("ConvertAttributeValueToQuestionnaireData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var roundTripped = (QuestionnaireData)convertMethod.Invoke(_service, new object[] { savedItem!["data"] })!;

        Assert.Equal(request.Data.Name, roundTripped.Name);
        Assert.Equal(request.Data.Description, roundTripped.Description);
        Assert.Equal(request.Data.Version, roundTripped.Version);
        Assert.Equal(request.Data.EstimatedTime, roundTripped.EstimatedTime);
        Assert.Equal(request.Data.Questions.Count, roundTripped.Questions.Count);
    }

    // -------------------------------
    // Batch Tests (Strongly Typed)
    // -------------------------------

    [Fact]
    public async Task CreateBatchAsync_ShouldProcessAllRequests_FromJson()
    {
        var data = LoadBatchImportData();

        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse { Item = null });
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ReturnsAsync(new PutItemResponse());

        var result = await _service.CreateBatchAsync(data, "testuser");

        Assert.Equal(data.Count, result.Summary.Processed);
        Assert.Equal(data.Count, result.Summary.Successful);
        Assert.Equal(0, result.Summary.Failed);
        Assert.All(result.Results, r => Assert.Equal("success", r.Status));
    }

    [Fact]
    public async Task CreateBatchAsync_ShouldContinue_WhenSomeRequestsFail()
    {
        var data = new List<CreateQuestionnaireRequest>
        {
            new() { Id = "good", Data = new QuestionnaireData { Name = "Valid Q" } },
            new() { Id = "bad", Data = null! } // force failure
        };

        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse { Item = null });
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ReturnsAsync(new PutItemResponse());

        var result = await _service.CreateBatchAsync(data, "testuser");

        Assert.Equal(2, result.Summary.Processed);
        Assert.Equal(1, result.Summary.Successful);
        Assert.Equal(1, result.Summary.Failed);
        Assert.Contains(result.Results, r => r.Status == "error");
    }

    // -------------------------------
    // Update Verification
    // -------------------------------

    [Fact]
    public async Task UpdateAsync_ShouldUpdateDataCorrectly()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "update_questionnaire.json");
        var json = File.ReadAllText(path);
        var data = JsonSerializer.Deserialize<QuestionnaireData>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        )!;

        UpdateItemRequest? capturedRequest = null;

        _mockDynamoClient.Setup(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .Callback<UpdateItemRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new UpdateItemResponse());

        await _service.UpdateAsync("PQ1", data, "testuser");

        Assert.NotNull(capturedRequest);
        var newData = capturedRequest!.ExpressionAttributeValues[":data"].M;

        Assert.Equal(data.Name, newData["name"].S);
        Assert.Equal(data.Description, newData["description"].S);
        Assert.Equal(data.Version, newData["version"].S);
        Assert.Equal(data.EstimatedTime.ToString(), newData["estimatedTime"].N);

        var questions = newData["questions"].L;
        Assert.NotEmpty(questions);
    }

    // -------------------------------
    // Helpers
    // -------------------------------

    private static List<CreateQuestionnaireRequest> LoadBatchImportData()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "questionnaires_batch_import.json");

        if (!File.Exists(path))
            throw new FileNotFoundException($"Missing test data file: {path}");

        var json = File.ReadAllText(path);
        var root = JsonDocument.Parse(json);
        var requests = new List<CreateQuestionnaireRequest>();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        foreach (var element in root.RootElement.GetProperty("requests").EnumerateArray())
        {
            var id = element.GetProperty("id").GetString() ?? "";
            var data = JsonSerializer.Deserialize<QuestionnaireData>(
                element.GetProperty("data").GetRawText(),
                options
            );

            if (data != null && !string.IsNullOrWhiteSpace(data.Name))
                requests.Add(new CreateQuestionnaireRequest { Id = id, Data = data });
        }

        if (requests.Count == 0)
            throw new InvalidOperationException("No valid questionnaire entries were loaded from test data.");

        return requests;
    }

}
