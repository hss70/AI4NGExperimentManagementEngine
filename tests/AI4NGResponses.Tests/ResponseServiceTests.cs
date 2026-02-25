using Moq;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGResponsesLambda.Services;
using AI4NGResponsesLambda.Models;

namespace AI4NGResponses.Tests;

public class ResponseServiceTests
{
    private readonly Mock<IAmazonDynamoDB> _mockDynamoClient;
    private readonly ResponseService _service;

    public ResponseServiceTests()
    {
        _mockDynamoClient = new Mock<IAmazonDynamoDB>();
        Environment.SetEnvironmentVariable("RESPONSES_TABLE", "responses-test");
        
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ReturnsAsync(new PutItemResponse());
            
        _service = new ResponseService(_mockDynamoClient.Object);
    }

    [Fact]
    public async Task CreateResponseAsync_ShouldReturnId_WhenValid()
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

        // Act
        var result = await _service.CreateResponseAsync(response, "testuser");

        // Assert
        Assert.NotNull(result);
        _mockDynamoClient.Verify(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task GetResponsesAsync_ShouldReturnResponses()
    {
        // Arrange
        var scanResponse = new ScanResponse
        {
            Items = new List<Dictionary<string, AttributeValue>>
            {
                new()
                {
                    ["PK"] = new AttributeValue("RESPONSE#test-id"),
                    ["data"] = new AttributeValue
                    {
                        M = new Dictionary<string, AttributeValue>
                        {
                            ["experimentId"] = new AttributeValue("test-experiment")
                        }
                    },
                    ["createdBy"] = new AttributeValue("testuser"),
                    ["createdAt"] = new AttributeValue("2023-11-01T09:00:00Z")
                }
            }
        };

        _mockDynamoClient.Setup(x => x.ScanAsync(It.IsAny<ScanRequest>(), default))
            .ReturnsAsync(scanResponse);

        // Act
        var result = await _service.GetResponsesAsync();

        // Assert
        Assert.Single(result);
        _mockDynamoClient.Verify(x => x.ScanAsync(It.IsAny<ScanRequest>(), default), Times.Once);
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

        _mockDynamoClient.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), default))
            .ReturnsAsync(queryResponse);

        // Act
        var result = await _service.GetResponsesAsync("test-experiment");

        // Assert
        Assert.Single(result);
        _mockDynamoClient.Verify(x => x.QueryAsync(It.IsAny<QueryRequest>(), default), Times.Once);
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

        _mockDynamoClient.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), default))
            .ReturnsAsync(queryResponse);

        // Act
        var result = await _service.GetResponsesAsync("test-experiment", "test-session");

        // Assert
        Assert.Single(result);
        _mockDynamoClient.Verify(x => x.QueryAsync(It.IsAny<QueryRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task GetResponseAsync_ShouldReturnResponse_WhenExists()
    {
        // Arrange
        var getResponse = new GetItemResponse
        {
            IsItemSet = true,
            Item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue("RESPONSE#test-id"),
                ["data"] = new AttributeValue { M = new Dictionary<string, AttributeValue> { ["experimentId"] = new AttributeValue("test-experiment") } },
                ["createdBy"] = new AttributeValue("testuser"),
                ["createdAt"] = new AttributeValue("2023-11-01T09:00:00Z")
            }
        };

        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(getResponse);

        // Act
        var result = await _service.GetResponseAsync("test-id");

        // Assert
        Assert.NotNull(result);
        _mockDynamoClient.Verify(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task GetResponseAsync_ShouldReturnNull_WhenNotExists()
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse { Item = null });

        // Act
        var result = await _service.GetResponseAsync("non-existent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateResponseAsync_ShouldCallUpdateItem()
    {
        // Arrange
        var data = new ResponseData { ExperimentId = "updated-experiment" };
        _mockDynamoClient.Setup(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .ReturnsAsync(new UpdateItemResponse());

        // Act
        await _service.UpdateResponseAsync("test-id", data, "testuser");

        // Assert
        _mockDynamoClient.Verify(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task DeleteResponseAsync_ShouldCallDeleteItem()
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.DeleteItemAsync(It.IsAny<DeleteItemRequest>(), default))
            .ReturnsAsync(new DeleteItemResponse());

        // Act
        await _service.DeleteResponseAsync("test-id", "testuser");

        // Assert
        _mockDynamoClient.Verify(x => x.DeleteItemAsync(It.IsAny<DeleteItemRequest>(), default), Times.Once);
    }
}