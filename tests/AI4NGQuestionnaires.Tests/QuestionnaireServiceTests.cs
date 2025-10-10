using Xunit;
using Moq;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGQuestionnairesLambda.Services;
using AI4NGQuestionnairesLambda.Models;

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

    [Fact]
    public async Task CreateAsync_ShouldReturnId_WhenValidRequest()
    {
        // Arrange
        var request = new CreateQuestionnaireRequest
        {
            Id = "test-id",
            Data = new QuestionnaireData { Name = "Test Questionnaire" }
        };

        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
        .ReturnsAsync(new GetItemResponse
        {
            Item = null
        });
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ReturnsAsync(new PutItemResponse());

        // Act
        var result = await _service.CreateAsync(request, "testuser");

        // Assert
        Assert.Equal("test-id", result);
        _mockDynamoClient.Verify(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnQuestionnaires_WhenDataExists()
    {
        // Arrange
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

        // Act
        var result = await _service.GetAllAsync();

        // Assert
        Assert.Single(result);
        _mockDynamoClient.Verify(x => x.ScanAsync(It.IsAny<ScanRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenNotFound()
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse { Item = null });

        // Act
        var result = await _service.GetByIdAsync("nonexistent");

        // Assert
        Assert.Null(result);
    }
}