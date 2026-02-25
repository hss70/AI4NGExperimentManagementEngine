using Moq;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGExperimentsLambda.Services;
using AI4NGExperimentsLambda.Models;

namespace AI4NGExperiments.Tests;

public class IntegrationTests
{
    private readonly Mock<IAmazonDynamoDB> _mockDynamoClient;
    private readonly ExperimentsService _service;

    public IntegrationTests()
    {
        _mockDynamoClient = new Mock<IAmazonDynamoDB>();
        Environment.SetEnvironmentVariable("EXPERIMENTS_TABLE", "experiments-test");
        Environment.SetEnvironmentVariable("RESPONSES_TABLE", "responses-test");
        Environment.SetEnvironmentVariable("QUESTIONNAIRES_TABLE", "questionnaires-test");

        _service = new ExperimentsService(_mockDynamoClient.Object);
    }

    [Fact(Skip = "Refactor: moved to Session/Membership services")]
    public async Task CompleteExperimentWorkflow_ShouldSucceed()
    {
        // Quarantined - moved to LegacyMonolith/session & membership services
        await Task.CompletedTask;
    }

    [Fact(Skip = "Refactor: moved to Session/Membership services")]
    public async Task ExperimentMemberManagementWorkflow_ShouldSucceed()
    {
        // Quarantined - moved to LegacyMonolith/membership services
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ErrorHandling_ShouldPropagateExceptions()
    {
        // Arrange - Mock DynamoDB failure
        // Service lists experiments using QueryAsync (GSI1)
        _mockDynamoClient.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), default))
            .ThrowsAsync(new AmazonDynamoDBException("DynamoDB connection failed"));

        // Act & Assert
        await Assert.ThrowsAsync<AmazonDynamoDBException>(
            () => _service.GetExperimentsAsync());
    }

    [Fact(Skip = "Refactor: moved to Session/Membership services")]
    public async Task ConcurrentOperations_ShouldHandleMultipleQueries()
    {
        // Quarantined - moved to LegacyMonolith/session services
        await Task.CompletedTask;
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