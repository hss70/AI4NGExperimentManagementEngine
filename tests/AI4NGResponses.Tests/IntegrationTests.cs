using Moq;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGResponsesLambda.Services;
using AI4NGResponsesLambda.Models;

namespace AI4NGResponses.Tests;

public class IntegrationTests
{
    private readonly Mock<IAmazonDynamoDB> _mockDynamoClient;
    private readonly ResponseService _service;

    public IntegrationTests()
    {
        _mockDynamoClient = new Mock<IAmazonDynamoDB>();
        Environment.SetEnvironmentVariable("RESPONSES_TABLE", "responses-test");
        
        _service = new ResponseService(_mockDynamoClient.Object);
    }

    [Fact]
    public async Task CompleteResponseWorkflow_ShouldSucceed()
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ReturnsAsync(new PutItemResponse());

        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse
            {
                IsItemSet = true,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue("RESPONSE#test-id"),
                    ["data"] = new AttributeValue { M = new Dictionary<string, AttributeValue> { ["experimentId"] = new AttributeValue("test-experiment") } },
                    ["createdBy"] = new AttributeValue("testuser"),
                    ["createdAt"] = new AttributeValue("2023-11-01T09:00:00Z")
                }
            });

        _mockDynamoClient.Setup(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .ReturnsAsync(new UpdateItemResponse());

        _mockDynamoClient.Setup(x => x.DeleteItemAsync(It.IsAny<DeleteItemRequest>(), default))
            .ReturnsAsync(new DeleteItemResponse());

        // Act - Step 1: Create response
        var response = new Response
        {
            Data = new ResponseData 
            { 
                ExperimentId = "test-experiment",
                SessionId = "test-session",
                QuestionnaireId = "test-questionnaire"
            }
        };
        var createResult = await _service.CreateResponseAsync(response, "testuser");

        // Act - Step 2: Retrieve response
        var retrievedResponse = await _service.GetResponseAsync("test-id");

        // Act - Step 3: Update response
        var updateData = new ResponseData { ExperimentId = "updated-experiment" };
        await _service.UpdateResponseAsync("test-id", updateData, "testuser");

        // Act - Step 4: Delete response
        await _service.DeleteResponseAsync("test-id", "testuser");

        // Assert
        Assert.NotNull(createResult);
        Assert.NotNull(retrievedResponse);
        
        _mockDynamoClient.Verify(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default), Times.Once);
        _mockDynamoClient.Verify(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default), Times.Once);
        _mockDynamoClient.Verify(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default), Times.Once);
        _mockDynamoClient.Verify(x => x.DeleteItemAsync(It.IsAny<DeleteItemRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task ResponseFilteringWorkflow_ShouldSucceed()
    {
        // Arrange
        var queryResponse = new QueryResponse
        {
            Items = new List<Dictionary<string, AttributeValue>>
            {
                new()
                {
                    ["PK"] = new AttributeValue("RESPONSE#test-id-1"),
                    ["data"] = new AttributeValue { M = new Dictionary<string, AttributeValue> { ["experimentId"] = new AttributeValue("test-experiment") } },
                    ["createdBy"] = new AttributeValue("testuser")
                },
                new()
                {
                    ["PK"] = new AttributeValue("RESPONSE#test-id-2"),
                    ["data"] = new AttributeValue { M = new Dictionary<string, AttributeValue> { ["sessionId"] = new AttributeValue("test-session") } },
                    ["createdBy"] = new AttributeValue("testuser")
                }
            }
        };

        _mockDynamoClient.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), default))
            .ReturnsAsync(queryResponse);

        // Act - Step 1: Get responses by experiment
        var experimentResponses = await _service.GetResponsesAsync("test-experiment");

        // Act - Step 2: Get responses by experiment and session
        var sessionResponses = await _service.GetResponsesAsync("test-experiment", "test-session");

        // Assert
        Assert.Equal(2, experimentResponses.Count());
        Assert.Equal(2, sessionResponses.Count());
        _mockDynamoClient.Verify(x => x.QueryAsync(It.IsAny<QueryRequest>(), default), Times.Exactly(2));
    }

    [Fact]
    public async Task ErrorHandling_ShouldPropagateExceptions()
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.ScanAsync(It.IsAny<ScanRequest>(), default))
            .ThrowsAsync(new AmazonDynamoDBException("DynamoDB connection failed"));

        // Act & Assert
        await Assert.ThrowsAsync<AmazonDynamoDBException>(
            () => _service.GetResponsesAsync());
    }

    [Fact]
    public async Task DataConsistency_ShouldMaintainAuditTrail()
    {
        // Arrange
        var response = new Response
        {
            Data = new ResponseData 
            { 
                ExperimentId = "audit-experiment",
                SessionId = "audit-session",
                QuestionnaireId = "audit-questionnaire"
            }
        };

        PutItemRequest? capturedRequest = null;
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .Callback<PutItemRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new PutItemResponse());

        // Act
        await _service.CreateResponseAsync(response, "audit-user");

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Item.ContainsKey("createdBy"));
        Assert.Equal("audit-user", capturedRequest.Item["createdBy"].S);
        Assert.True(capturedRequest.Item.ContainsKey("createdAt"));
        
        var timestamp = capturedRequest.Item["createdAt"].S;
        Assert.True(DateTime.TryParse(timestamp, out _), "Timestamp should be valid DateTime");
    }
}