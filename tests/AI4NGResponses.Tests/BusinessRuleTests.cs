using Xunit;
using Moq;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGResponsesLambda.Services;
using AI4NGResponsesLambda.Models;

namespace AI4NGResponses.Tests;

public class BusinessRuleTests
{
    private readonly Mock<IAmazonDynamoDB> _mockDynamoClient;
    private readonly ResponseService _service;

    public BusinessRuleTests()
    {
        _mockDynamoClient = new Mock<IAmazonDynamoDB>();
        Environment.SetEnvironmentVariable("RESPONSES_TABLE", "responses-test");
        
        _service = new ResponseService(_mockDynamoClient.Object);
    }

    [Fact]
    public async Task CreateResponseAsync_ShouldValidateRequiredFields()
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
        var result = await _service.CreateResponseAsync(response, "testuser");

        // Assert
        Assert.NotNull(result);
        _mockDynamoClient.Verify(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task UpdateResponseAsync_ShouldPreserveMetadata()
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
        Assert.Contains(":updatedBy", capturedRequest.ExpressionAttributeValues.Keys);
        Assert.Contains(":timestamp", capturedRequest.ExpressionAttributeValues.Keys);
        Assert.Equal("testuser", capturedRequest.ExpressionAttributeValues[":updatedBy"].S);
    }

    [Fact]
    public async Task GetResponsesAsync_ShouldFilterByExperiment_WhenProvided()
    {
        // Arrange
        var queryResponse = new QueryResponse
        {
            Items = new List<Dictionary<string, AttributeValue>>
            {
                new()
                {
                    ["PK"] = new AttributeValue("RESPONSE#test-id"),
                    ["data"] = new AttributeValue { M = new Dictionary<string, AttributeValue> { ["experimentId"] = new AttributeValue("test-experiment") } },
                    ["createdBy"] = new AttributeValue("testuser")
                }
            }
        };

        QueryRequest? capturedRequest = null;
        _mockDynamoClient.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), default))
            .Callback<QueryRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(queryResponse);

        // Act
        var result = await _service.GetResponsesAsync("test-experiment");

        // Assert
        Assert.Single(result);
        Assert.NotNull(capturedRequest);
        Assert.Equal("GSI1", capturedRequest.IndexName);
        Assert.Contains("EXPERIMENT#test-experiment", capturedRequest.ExpressionAttributeValues[":pk"].S);
    }

    [Fact]
    public async Task GetResponsesAsync_ShouldFilterBySession_WhenProvided()
    {
        // Arrange
        var queryResponse = new QueryResponse
        {
            Items = new List<Dictionary<string, AttributeValue>>
            {
                new()
                {
                    ["PK"] = new AttributeValue("RESPONSE#test-id"),
                    ["data"] = new AttributeValue { M = new Dictionary<string, AttributeValue> { ["sessionId"] = new AttributeValue("test-session") } },
                    ["createdBy"] = new AttributeValue("testuser")
                }
            }
        };

        QueryRequest? capturedRequest = null;
        _mockDynamoClient.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), default))
            .Callback<QueryRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(queryResponse);

        // Act
        var result = await _service.GetResponsesAsync("test-experiment", "test-session");

        // Assert
        Assert.Single(result);
        Assert.NotNull(capturedRequest);
        Assert.Equal("GSI1", capturedRequest.IndexName);
        Assert.Contains("EXPERIMENT#test-experiment", capturedRequest.ExpressionAttributeValues[":pk"].S);
        Assert.Contains("SESSION#test-session", capturedRequest.ExpressionAttributeValues[":sk"].S);
    }

    [Fact]
    public async Task GetResponsesAsync_ShouldUseScan_WhenNoFilters()
    {
        // Arrange
        var scanResponse = new ScanResponse
        {
            Items = new List<Dictionary<string, AttributeValue>>
            {
                new()
                {
                    ["PK"] = new AttributeValue("RESPONSE#test-id"),
                    ["data"] = new AttributeValue { M = new Dictionary<string, AttributeValue>() },
                    ["createdBy"] = new AttributeValue("testuser")
                }
            }
        };

        ScanRequest? capturedRequest = null;
        _mockDynamoClient.Setup(x => x.ScanAsync(It.IsAny<ScanRequest>(), default))
            .Callback<ScanRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(scanResponse);

        // Act
        var result = await _service.GetResponsesAsync();

        // Assert
        Assert.Single(result);
        Assert.NotNull(capturedRequest);
        Assert.Equal("#type = :type", capturedRequest.FilterExpression);
        Assert.Equal("Response", capturedRequest.ExpressionAttributeValues[":type"].S);
    }

    [Fact]
    public async Task CreateResponseAsync_ShouldGenerateUniqueId()
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

        var capturedIds = new List<string>();
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .Callback<PutItemRequest, CancellationToken>((request, _) => 
            {
                var id = request.Item["PK"].S.Replace("RESPONSE#", "");
                capturedIds.Add(id);
            })
            .ReturnsAsync(new PutItemResponse());

        // Act
        var result1 = await _service.CreateResponseAsync(response, "testuser");
        var result2 = await _service.CreateResponseAsync(response, "testuser");

        // Assert
        Assert.Equal(2, capturedIds.Count);
        Assert.NotEqual(capturedIds[0], capturedIds[1]);
    }

    [Fact]
    public async Task DeleteResponseAsync_ShouldNotValidateExistence()
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.DeleteItemAsync(It.IsAny<DeleteItemRequest>(), default))
            .ReturnsAsync(new DeleteItemResponse());

        // Act - Should not throw even if response doesn't exist
        await _service.DeleteResponseAsync("non-existent", "testuser");

        // Assert
        _mockDynamoClient.Verify(x => x.DeleteItemAsync(It.IsAny<DeleteItemRequest>(), default), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task GetResponsesAsync_ShouldHandleInvalidFilters(string invalidFilter)
    {
        // Arrange
        var scanResponse = new ScanResponse { Items = new List<Dictionary<string, AttributeValue>>() };
        _mockDynamoClient.Setup(x => x.ScanAsync(It.IsAny<ScanRequest>(), default))
            .ReturnsAsync(scanResponse);

        // Act
        var result = await _service.GetResponsesAsync(invalidFilter);

        // Assert
        Assert.Empty(result);
        _mockDynamoClient.Verify(x => x.ScanAsync(It.IsAny<ScanRequest>(), default), Times.Once);
    }
}