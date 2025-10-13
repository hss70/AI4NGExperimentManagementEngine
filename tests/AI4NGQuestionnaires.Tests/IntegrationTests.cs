using Xunit;
using Moq;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGQuestionnairesLambda.Services;
using AI4NGQuestionnairesLambda.Models;

namespace AI4NGQuestionnaires.Tests;

public class IntegrationTests
{
    private readonly Mock<IAmazonDynamoDB> _mockDynamoClient;
    private readonly QuestionnaireService _service;

    public IntegrationTests()
    {
        _mockDynamoClient = new Mock<IAmazonDynamoDB>();
        Environment.SetEnvironmentVariable("QUESTIONNAIRES_TABLE", "test-table");
        
        _service = new QuestionnaireService(_mockDynamoClient.Object);
    }

    [Fact]
    public async Task CompleteQuestionnaireWorkflow_ShouldSucceed()
    {
        // Arrange
        var uniqueId = $"integration-test-{Guid.NewGuid()}";
        
        // Mock sequence: first call returns null (no duplicate), second call returns item (after creation)
        _mockDynamoClient.SetupSequence(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse { Item = null }) // First call - no duplicate
            .ReturnsAsync(new GetItemResponse // Second call - item exists after creation
            {
                Item = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue($"QUESTIONNAIRE#{uniqueId}"),
                    ["data"] = new AttributeValue { M = new Dictionary<string, AttributeValue> { ["name"] = new AttributeValue("Test") } },
                    ["createdBy"] = new AttributeValue("testuser"),
                    ["createdAt"] = new AttributeValue("2023-11-01T09:00:00Z")
                }
            });
        
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ReturnsAsync(new PutItemResponse());
        _mockDynamoClient.Setup(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .ReturnsAsync(new UpdateItemResponse());

        // Act - Step 1: Create questionnaire
        var request = new CreateQuestionnaireRequest
        {
            Id = uniqueId,
            Data = new QuestionnaireData { Name = "Integration Test Questionnaire" }
        };
        var createResult = await _service.CreateAsync(request, "testuser");

        // Act - Step 2: Retrieve questionnaire
        var retrievedQuestionnaire = await _service.GetByIdAsync(uniqueId);

        // Act - Step 3: Update questionnaire
        var updateData = new QuestionnaireData { Name = "Updated Questionnaire" };
        await _service.UpdateAsync(uniqueId, updateData, "testuser");

        // Act - Step 4: Delete questionnaire
        await _service.DeleteAsync(uniqueId, "testuser");

        // Assert
        Assert.NotNull(createResult);
        Assert.NotNull(retrievedQuestionnaire);
        
        _mockDynamoClient.Verify(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default), Times.Once);
        _mockDynamoClient.Verify(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default), Times.AtLeast(2));
        _mockDynamoClient.Verify(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default), Times.Exactly(2)); // Update + Delete
    }

    [Fact]
    public async Task QuestionnaireListingWorkflow_ShouldSucceed()
    {
        // Arrange
        var scanResponse = new ScanResponse
        {
            Items = new List<Dictionary<string, AttributeValue>>
            {
                new()
                {
                    ["PK"] = new AttributeValue("QUESTIONNAIRE#test-1"),
                    ["data"] = new AttributeValue { M = new Dictionary<string, AttributeValue> { ["name"] = new AttributeValue("Test 1") } }
                },
                new()
                {
                    ["PK"] = new AttributeValue("QUESTIONNAIRE#test-2"),
                    ["data"] = new AttributeValue { M = new Dictionary<string, AttributeValue> { ["name"] = new AttributeValue("Test 2") } }
                }
            }
        };

        _mockDynamoClient.Setup(x => x.ScanAsync(It.IsAny<ScanRequest>(), default))
            .ReturnsAsync(scanResponse);

        // Act
        var questionnaires = await _service.GetAllAsync();

        // Assert
        Assert.Equal(2, questionnaires.Count());
        _mockDynamoClient.Verify(x => x.ScanAsync(It.IsAny<ScanRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task ErrorHandling_ShouldPropagateExceptions()
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.ScanAsync(It.IsAny<ScanRequest>(), default))
            .ThrowsAsync(new AmazonDynamoDBException("DynamoDB connection failed"));

        // Act & Assert
        await Assert.ThrowsAsync<AmazonDynamoDBException>(
            () => _service.GetAllAsync());
    }

    [Fact]
    public async Task DataConsistency_ShouldMaintainAuditTrail()
    {
        // Arrange
        var request = new CreateQuestionnaireRequest
        {
            Id = "audit-test",
            Data = new QuestionnaireData { Name = "Audit Test Questionnaire" }
        };

        PutItemRequest? capturedRequest = null;
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse { Item = null });
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .Callback<PutItemRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new PutItemResponse());

        // Act
        await _service.CreateAsync(request, "audit-user");

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Item.ContainsKey("createdBy"));
        Assert.Equal("audit-user", capturedRequest.Item["createdBy"].S);
        Assert.True(capturedRequest.Item.ContainsKey("createdAt"));
        
        var timestamp = capturedRequest.Item["createdAt"].S;
        Assert.True(DateTime.TryParse(timestamp, out _), "Timestamp should be valid DateTime");
    }
}