using Moq;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGResponsesLambda.Services;
using AI4NGResponsesLambda.Models;

namespace AI4NGResponses.Tests;

public class DynamoDBFormatTests
{
    private readonly Mock<IAmazonDynamoDB> _mockDynamoClient;
    private readonly ResponseService _service;

    public DynamoDBFormatTests()
    {
        _mockDynamoClient = new Mock<IAmazonDynamoDB>();
        Environment.SetEnvironmentVariable("RESPONSES_TABLE", "responses-test");
        
        _service = new ResponseService(_mockDynamoClient.Object);
    }

    [Fact]
    public async Task CreateResponseAsync_ShouldCreateCorrectPKFormat()
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

        PutItemRequest? capturedRequest = null;
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .Callback<PutItemRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new PutItemResponse());

        // Act
        await _service.CreateResponseAsync(response, "testuser");

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Item.ContainsKey("PK"));
        Assert.StartsWith("RESPONSE#", capturedRequest.Item["PK"].S);
    }

    [Fact]
    public async Task CreateResponseAsync_ShouldCreateCorrectSKFormat()
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

        PutItemRequest? capturedRequest = null;
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .Callback<PutItemRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new PutItemResponse());

        // Act
        await _service.CreateResponseAsync(response, "testuser");

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Item.ContainsKey("SK"));
        Assert.Equal("METADATA", capturedRequest.Item["SK"].S);
    }

    [Fact]
    public async Task CreateResponseAsync_ShouldSetCorrectTypeField()
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

        PutItemRequest? capturedRequest = null;
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .Callback<PutItemRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new PutItemResponse());

        // Act
        await _service.CreateResponseAsync(response, "testuser");

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Item.ContainsKey("type"));
        Assert.Equal("Response", capturedRequest.Item["type"].S);
    }

    [Fact]
    public async Task CreateResponseAsync_ShouldCreateCorrectGSI1Structure()
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

        PutItemRequest? capturedRequest = null;
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .Callback<PutItemRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new PutItemResponse());

        // Act
        await _service.CreateResponseAsync(response, "testuser");

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Item.ContainsKey("GSI1PK"));
        Assert.Equal("EXPERIMENT#test-experiment", capturedRequest.Item["GSI1PK"].S);
        Assert.True(capturedRequest.Item.ContainsKey("GSI1SK"));
        Assert.Equal("SESSION#test-session", capturedRequest.Item["GSI1SK"].S);
    }

    [Fact]
    public async Task CreateResponseAsync_ShouldIncludeAuditFields()
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

        PutItemRequest? capturedRequest = null;
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .Callback<PutItemRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new PutItemResponse());

        // Act
        await _service.CreateResponseAsync(response, "testuser");

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Item.ContainsKey("createdBy"));
        Assert.Equal("testuser", capturedRequest.Item["createdBy"].S);
        Assert.True(capturedRequest.Item.ContainsKey("createdAt"));
        Assert.NotEmpty(capturedRequest.Item["createdAt"].S);
    }

    [Fact]
    public async Task UpdateResponseAsync_ShouldUseCorrectKeyFormat()
    {
        // Arrange
        var data = new ResponseData { ExperimentId = "updated-experiment" };

        UpdateItemRequest? capturedRequest = null;
        _mockDynamoClient.Setup(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .Callback<UpdateItemRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new UpdateItemResponse());

        // Act
        await _service.UpdateResponseAsync("test-id", data, "testuser");

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Key.ContainsKey("PK"));
        Assert.Equal("RESPONSE#test-id", capturedRequest.Key["PK"].S);
        Assert.True(capturedRequest.Key.ContainsKey("SK"));
        Assert.Equal("METADATA", capturedRequest.Key["SK"].S);
    }

    [Fact]
    public async Task DeleteResponseAsync_ShouldUseCorrectKeyFormat()
    {
        // Arrange
        DeleteItemRequest? capturedRequest = null;
        _mockDynamoClient.Setup(x => x.DeleteItemAsync(It.IsAny<DeleteItemRequest>(), default))
            .Callback<DeleteItemRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new DeleteItemResponse());

        // Act
        await _service.DeleteResponseAsync("test-id", "testuser");

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Key.ContainsKey("PK"));
        Assert.Equal("RESPONSE#test-id", capturedRequest.Key["PK"].S);
        Assert.True(capturedRequest.Key.ContainsKey("SK"));
        Assert.Equal("METADATA", capturedRequest.Key["SK"].S);
    }

    [Fact]
    public async Task GetResponseAsync_ShouldUseCorrectKeyFormat()
    {
        // Arrange
        GetItemRequest? capturedRequest = null;
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .Callback<GetItemRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new GetItemResponse { IsItemSet = false });

        // Act
        await _service.GetResponseAsync("test-id");

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Key.ContainsKey("PK"));
        Assert.Equal("RESPONSE#test-id", capturedRequest.Key["PK"].S);
        Assert.True(capturedRequest.Key.ContainsKey("SK"));
        Assert.Equal("METADATA", capturedRequest.Key["SK"].S);
    }
}