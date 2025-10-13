using Xunit;
using Moq;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGExperimentsLambda.Services;
using AI4NGExperimentsLambda.Models;

namespace AI4NGExperiments.Tests;

public class IntegrationTests
{
    private readonly Mock<IAmazonDynamoDB> _mockDynamoClient;
    private readonly ExperimentService _service;

    public IntegrationTests()
    {
        _mockDynamoClient = new Mock<IAmazonDynamoDB>();
        Environment.SetEnvironmentVariable("EXPERIMENTS_TABLE", "experiments-test");
        Environment.SetEnvironmentVariable("RESPONSES_TABLE", "responses-test");
        Environment.SetEnvironmentVariable("QUESTIONNAIRES_TABLE", "questionnaires-test");
        
        _service = new ExperimentService(_mockDynamoClient.Object);
    }

    [Fact]
    public async Task CompleteExperimentWorkflow_ShouldSucceed()
    {
        // Arrange - Mock questionnaire exists
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.Is<GetItemRequest>(r => r.Key["PK"].S.Contains("QUESTIONNAIRE")), default))
            .ReturnsAsync(new GetItemResponse 
            { 
                IsItemSet = true,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue("QUESTIONNAIRE#test-questionnaire"),
                    ["type"] = new AttributeValue("Questionnaire")
                }
            });

        // Mock experiment creation
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ReturnsAsync(new PutItemResponse());

        // Mock experiment retrieval
        _mockDynamoClient.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), default))
            .ReturnsAsync(new QueryResponse
            {
                Items = new List<Dictionary<string, AttributeValue>>
                {
                    new()
                    {
                        ["PK"] = new AttributeValue("EXPERIMENT#test-id"),
                        ["SK"] = new AttributeValue("METADATA"),
                        ["data"] = new AttributeValue { M = new Dictionary<string, AttributeValue> { ["name"] = new AttributeValue("Test Experiment") } },
                        ["questionnaireConfig"] = new AttributeValue { M = new Dictionary<string, AttributeValue>() },
                        ["updatedAt"] = new AttributeValue(DateTime.UtcNow.ToString("O"))
                    }
                }
            });

        // Mock experiment exists for sync
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.Is<GetItemRequest>(r => r.Key["PK"].S.Contains("EXPERIMENT")), default))
            .ReturnsAsync(new GetItemResponse 
            { 
                IsItemSet = true,
                Item = new Dictionary<string, AttributeValue> { ["PK"] = new AttributeValue("EXPERIMENT#test-id") }
            });

        // Act - Step 1: Create experiment
        var experiment = new Experiment
        {
            Data = new ExperimentData { Name = "Integration Test Experiment" },
            QuestionnaireConfig = new QuestionnaireConfig { QuestionnaireIds = new List<string> { "test-questionnaire" } }
        };
        var createResult = await _service.CreateExperimentAsync(experiment, "testuser");

        // Act - Step 2: Retrieve experiment
        var retrievedExperiment = await _service.GetExperimentAsync("test-id");

        // Act - Step 3: Sync experiment data
        var lastSyncTime = DateTime.UtcNow.AddHours(-1);
        var syncResult = await _service.SyncExperimentAsync("test-id", lastSyncTime, "testuser");
        Assert.NotNull(syncResult);

        // Act - Step 4: Add member
        var memberData = new MemberRequest { Role = "participant" };
        await _service.AddMemberAsync("test-id", "user123", memberData, "testuser");

        // Assert
        Assert.NotNull(createResult);
        Assert.NotNull(retrievedExperiment);
        
        // Verify all operations were called
        _mockDynamoClient.Verify(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default), Times.AtLeast(2));
        _mockDynamoClient.Verify(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default), Times.AtLeast(2));
        _mockDynamoClient.Verify(x => x.QueryAsync(It.IsAny<QueryRequest>(), default), Times.Exactly(2));
    }

    [Fact]
    public async Task ExperimentMemberManagementWorkflow_ShouldSucceed()
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ReturnsAsync(new PutItemResponse());
        
        _mockDynamoClient.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), default))
            .ReturnsAsync(new QueryResponse
            {
                Items = new List<Dictionary<string, AttributeValue>>
                {
                    new()
                    {
                        ["SK"] = new AttributeValue("MEMBER#user123"),
                        ["role"] = new AttributeValue("participant"),
                        ["addedAt"] = new AttributeValue("2024-01-01T00:00:00Z")
                    }
                }
            });

        _mockDynamoClient.Setup(x => x.DeleteItemAsync(It.IsAny<DeleteItemRequest>(), default))
            .ReturnsAsync(new DeleteItemResponse());

        // Act - Step 1: Add member
        var memberData = new MemberRequest { Role = "participant" };
        await _service.AddMemberAsync("test-id", "user123", memberData, "testuser");

        // Act - Step 2: Get members
        var members = await _service.GetExperimentMembersAsync("test-id");

        // Act - Step 3: Remove member
        await _service.RemoveMemberAsync("test-id", "user123", "testuser");

        // Assert
        Assert.Single(members);
        _mockDynamoClient.Verify(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default), Times.AtLeastOnce);
        _mockDynamoClient.Verify(x => x.QueryAsync(It.IsAny<QueryRequest>(), default), Times.AtLeastOnce);
        _mockDynamoClient.Verify(x => x.DeleteItemAsync(It.IsAny<DeleteItemRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task ErrorHandling_ShouldPropagateExceptions()
    {
        // Arrange - Mock DynamoDB failure
        _mockDynamoClient.Setup(x => x.ScanAsync(It.IsAny<ScanRequest>(), default))
            .ThrowsAsync(new AmazonDynamoDBException("DynamoDB connection failed"));

        // Act & Assert
        await Assert.ThrowsAsync<AmazonDynamoDBException>(
            () => _service.GetExperimentsAsync());
    }

    [Fact]
    public async Task ConcurrentOperations_ShouldHandleMultipleQueries()
    {
        // Arrange
        var lastSyncTime = DateTime.UtcNow.AddHours(-1);

        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse 
            { 
                IsItemSet = true,
                Item = new Dictionary<string, AttributeValue> { ["PK"] = new AttributeValue("EXPERIMENT#test-id") }
            });

        _mockDynamoClient.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), default))
            .ReturnsAsync(new QueryResponse
            {
                Items = new List<Dictionary<string, AttributeValue>>
                {
                    new() {
                        ["PK"] = new AttributeValue("EXPERIMENT#test-id"),
                        ["SK"] = new AttributeValue("SESSION#session-1"),
                        ["data"] = new AttributeValue { M = new Dictionary<string, AttributeValue>() }
                    },
                    new() {
                        ["PK"] = new AttributeValue("EXPERIMENT#test-id"),
                        ["SK"] = new AttributeValue("SESSION#session-2"),
                        ["data"] = new AttributeValue { M = new Dictionary<string, AttributeValue>() }
                    }
                }
            });

        // Act
        var result = await _service.SyncExperimentAsync("test-id", lastSyncTime, "testuser");

        // Assert
        Assert.NotNull(result);
        _mockDynamoClient.Verify(x => x.QueryAsync(It.IsAny<QueryRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task DataConsistency_ShouldMaintainAuditTrail()
    {
        // Arrange
        var experiment = new Experiment
        {
            Data = new ExperimentData { Name = "Audit Test Experiment" },
            QuestionnaireConfig = new QuestionnaireConfig()
        };

        PutItemRequest? capturedRequest = null;
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .Callback<PutItemRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new PutItemResponse());

        // Act
        await _service.CreateExperimentAsync(experiment, "audit-user");

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Item.ContainsKey("createdBy"));
        Assert.Equal("audit-user", capturedRequest.Item["createdBy"].S);
        Assert.True(capturedRequest.Item.ContainsKey("createdAt"));
        
        // Verify timestamp format (ISO 8601)
        var timestamp = capturedRequest.Item["createdAt"].S;
        Assert.True(DateTime.TryParse(timestamp, out _), "Timestamp should be valid DateTime");
    }
}