using Moq;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGResponsesLambda.Services;
using AI4NGResponsesLambda.Models;

namespace AI4NGResponses.Tests;

public class ValidationTests
{
    private readonly Mock<IAmazonDynamoDB> _mockDynamoClient;
    private readonly ResponseService _service;

    public ValidationTests()
    {
        _mockDynamoClient = new Mock<IAmazonDynamoDB>();
        Environment.SetEnvironmentVariable("RESPONSES_TABLE", "responses-test");

        _service = new ResponseService(_mockDynamoClient.Object);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task CreateResponseAsync_ShouldHandleInvalidExperimentId(string? invalidExperimentId)
    {
        // Arrange
        var response = new Response
        {
            Data = new ResponseData
            {
                ExperimentId = invalidExperimentId!,
                SessionId = "test-session",
                QuestionnaireId = "test-questionnaire"
            }
        };

        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ReturnsAsync(new PutItemResponse());

        // Act
        var result = await _service.CreateResponseAsync(response, "testuser");

        // Assert
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task CreateResponseAsync_ShouldHandleInvalidSessionId(string? invalidSessionId)
    {
        // Arrange
        var response = new Response
        {
            Data = new ResponseData
            {
                ExperimentId = "test-experiment",
                SessionId = invalidSessionId!,
                QuestionnaireId = "test-questionnaire"
            }
        };

        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ReturnsAsync(new PutItemResponse());

        // Act
        var result = await _service.CreateResponseAsync(response, "testuser");

        // Assert
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task CreateResponseAsync_ShouldHandleInvalidQuestionnaireId(string? invalidQuestionnaireId)
    {
        // Arrange
        var response = new Response
        {
            Data = new ResponseData
            {
                ExperimentId = "test-experiment",
                SessionId = "test-session",
                QuestionnaireId = invalidQuestionnaireId!
            }
        };

        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ReturnsAsync(new PutItemResponse());

        // Act
        var result = await _service.CreateResponseAsync(response, "testuser");

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task CreateResponseAsync_ShouldSucceed_WhenValidData()
    {
        // Arrange
        var response = new Response
        {
            Data = new ResponseData
            {
                ExperimentId = "valid-experiment",
                SessionId = "valid-session",
                QuestionnaireId = "valid-questionnaire"
            }
        };

        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ReturnsAsync(new PutItemResponse());

        // Act
        var result = await _service.CreateResponseAsync(response, "testuser");

        // Assert
        Assert.NotNull(result);
        _mockDynamoClient.Verify(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task GetResponseAsync_ShouldHandleInvalidResponseIds(string? invalidId)
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse { Item = null });

        // Act
        var result = await _service.GetResponseAsync(invalidId);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task CreateResponseAsync_ShouldHandleInvalidUsernames(string? username)
    {
        // Arrange
        var response = new Response
        {
            Data = new ResponseData
            {
                ExperimentId = "test-experiment",
                SessionId = "test-session",
                QuestionnaireId = "test-questionnaire"
            }
        };

        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ReturnsAsync(new PutItemResponse());

        // Act
        var result = await _service.CreateResponseAsync(response, username!);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetResponsesAsync_ShouldHandleEmptyFilters()
    {
        // Arrange
        var scanResponse = new ScanResponse
        {
            Items = new List<Dictionary<string, AttributeValue>>()
        };

        _mockDynamoClient.Setup(x => x.ScanAsync(It.IsAny<ScanRequest>(), default))
            .ReturnsAsync(scanResponse);

        // Act
        var result = await _service.GetResponsesAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetResponsesAsync_ShouldHandleNullFilters()
    {
        // Arrange
        var scanResponse = new ScanResponse
        {
            Items = new List<Dictionary<string, AttributeValue>>()
        };

        _mockDynamoClient.Setup(x => x.ScanAsync(It.IsAny<ScanRequest>(), default))
            .ReturnsAsync(scanResponse);

        // Act
        var result = await _service.GetResponsesAsync(null, null);

        // Assert
        Assert.Empty(result);
    }
}