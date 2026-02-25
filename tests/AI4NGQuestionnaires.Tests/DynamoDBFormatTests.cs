using Xunit;
using Moq;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGQuestionnairesLambda.Services;
using AI4NG.ExperimentManagement.Contracts.Questionnaires;

namespace AI4NGQuestionnaires.Tests;

public class DynamoDBFormatTests
{
    private readonly Mock<IAmazonDynamoDB> _mockDynamoClient;
    private readonly QuestionnaireService _service;

    public DynamoDBFormatTests()
    {
        _mockDynamoClient = new Mock<IAmazonDynamoDB>();
        Environment.SetEnvironmentVariable("QUESTIONNAIRES_TABLE", "test-table");
        _service = new QuestionnaireService(_mockDynamoClient.Object);
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateCorrectDynamoDBFormat()
    {
        // Arrange
        var request = new CreateQuestionnaireRequest
        {
            Id = "test-questionnaire-001",
            Data = new QuestionnaireDataDto
            {
                Name = "Test Questionnaire",
                Description = "Test Description",
                Questions = new List<QuestionDto>
                {
                    new() { Id = "q1", Text = "Test question?", Type = "text", Required = true }
                }
            }
        };

        PutItemRequest? capturedRequest = null;

        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse { Item = null });
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .Callback<PutItemRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new PutItemResponse());

        // Act
        await _service.CreateAsync(request.Id, request.Data, "testuser");

        // Assert
        Assert.NotNull(capturedRequest);
        var item = capturedRequest.Item;

        // Validate PK format
        Assert.Equal("QUESTIONNAIRE#test-questionnaire-001", item["PK"].S);

        // Validate SK format
        Assert.Equal("CONFIG", item["SK"].S);

        // Validate type field
        Assert.Equal("Questionnaire", item["type"].S);

        // Validate data structure
        Assert.True(item.ContainsKey("data"));
        Assert.True(item["data"].M.ContainsKey("name"));
        Assert.Equal("Test Questionnaire", item["data"].M["name"].S);

        // Validate metadata fields
        Assert.Equal("testuser", item["createdBy"].S);
        Assert.Equal("testuser", item["updatedBy"].S);
        Assert.True(item.ContainsKey("createdAt"));
        Assert.True(item.ContainsKey("updatedAt"));

        // Validate timestamp format (ISO 8601)
        Assert.True(DateTime.TryParse(item["createdAt"].S, out _));
    }

    [Fact]
    public async Task CreateAsync_ShouldValidateQuestionStructure()
    {
        // Arrange
        var request = new CreateQuestionnaireRequest
        {
            Id = "test-questionnaire-002",
            Data = new QuestionnaireDataDto
            {
                Name = "Test Questionnaire",
                Questions = new List<QuestionDto>
                {
                    new() { Id = "q1", Text = "Question 1", Type = "text", Required = true },
                    new() { Id = "q2", Text = "Question 2", Type = "select", Required = false, Options = new List<string> { "Option1", "Option2" } }
                }
            }
        };

        PutItemRequest? capturedRequest = null;
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse { Item = null });
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .Callback<PutItemRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new PutItemResponse());

        // Act
        await _service.CreateAsync(request.Id, request.Data, "testuser");

        // Assert
        var questionsArray = capturedRequest!.Item["data"].M["questions"].L;
        Assert.Equal(2, questionsArray.Count);

        // Validate first question structure
        var q1 = questionsArray[0].M;
        Assert.Equal("q1", q1["id"].S);
        Assert.Equal("Question 1", q1["text"].S);
        Assert.Equal("text", q1["type"].S);
        Assert.True(q1["required"].BOOL);

        // Validate second question with options
        var q2 = questionsArray[1].M;
        Assert.Equal("q2", q2["id"].S);
        Assert.Equal("select", q2["type"].S);
        Assert.False(q2["required"].BOOL);
        Assert.Equal(2, q2["options"].L.Count);
    }
}