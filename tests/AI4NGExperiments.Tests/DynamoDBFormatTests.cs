using Moq;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGExperimentsLambda.Services;

using AI4NGExperimentsLambda.Models;
using AI4NGExperimentsLambda.Models.Dtos;

namespace AI4NGExperiments.Tests;

public class DynamoDBFormatTests
{
    private readonly Mock<IAmazonDynamoDB> _mockDynamoClient;
    private readonly ExperimentsService _service;

    public DynamoDBFormatTests()
    {
        _mockDynamoClient = new Mock<IAmazonDynamoDB>();
        Environment.SetEnvironmentVariable("EXPERIMENTS_TABLE", "experiments-test");
        Environment.SetEnvironmentVariable("RESPONSES_TABLE", "responses-test");
        Environment.SetEnvironmentVariable("QUESTIONNAIRES_TABLE", "questionnaires-test");

        _service = new ExperimentsService(_mockDynamoClient.Object);
    }

    [Fact]
    public async Task CreateExperimentAsync_ShouldCreateCorrectPKFormat()
    {
        // Arrange
        var experiment = new Experiment
        {
            Data = new ExperimentData { Name = "Test Experiment" },
            QuestionnaireConfig = new QuestionnaireConfig()
        };

        PutItemRequest? capturedRequest = null;
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .Callback<PutItemRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new PutItemResponse());

        // Act
        await _service.CreateExperimentAsync(experiment, "testuser");

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Item.ContainsKey("PK"));
        Assert.StartsWith("EXPERIMENT#", capturedRequest.Item["PK"].S);
    }

    [Fact]
    public async Task CreateExperimentAsync_ShouldCreateCorrectSKFormat()
    {
        // Arrange
        var experiment = new Experiment
        {
            Data = new ExperimentData { Name = "Test Experiment" },
            QuestionnaireConfig = new QuestionnaireConfig()
        };

        PutItemRequest? capturedRequest = null;
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .Callback<PutItemRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new PutItemResponse());

        // Act
        await _service.CreateExperimentAsync(experiment, "testuser");

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Item.ContainsKey("SK"));
        Assert.Equal("METADATA", capturedRequest.Item["SK"].S);
    }

    [Fact]
    public async Task CreateExperimentAsync_ShouldSetCorrectTypeField()
    {
        // Arrange
        var experiment = new Experiment
        {
            Data = new ExperimentData { Name = "Test Experiment" },
            QuestionnaireConfig = new QuestionnaireConfig()
        };

        PutItemRequest? capturedRequest = null;
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .Callback<PutItemRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new PutItemResponse());

        // Act
        await _service.CreateExperimentAsync(experiment, "testuser");

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Item.ContainsKey("type"));
        Assert.Equal("Experiment", capturedRequest.Item["type"].S);
    }

    [Fact]
    public async Task CreateExperimentAsync_ShouldIncludeAuditFields()
    {
        // Arrange
        var experiment = new Experiment
        {
            Data = new ExperimentData { Name = "Test Experiment" },
            QuestionnaireConfig = new QuestionnaireConfig()
        };

        PutItemRequest? capturedRequest = null;
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .Callback<PutItemRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new PutItemResponse());

        // Act
        await _service.CreateExperimentAsync(experiment, "testuser");

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Item.ContainsKey("createdBy"));
        Assert.Equal("testuser", capturedRequest.Item["createdBy"].S);
        Assert.True(capturedRequest.Item.ContainsKey("createdAt"));
        Assert.NotEmpty(capturedRequest.Item["createdAt"].S);
    }

    [Fact]
    public async Task CreateExperimentAsync_ShouldSetGsi1FieldsAndReturnId()
    {
        // Arrange
        var experiment = new Experiment
        {
            Data = new ExperimentData { Name = "Test Experiment" },
            QuestionnaireConfig = new QuestionnaireConfig()
        };

        PutItemRequest? capturedRequest = null;
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .Callback<PutItemRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new PutItemResponse());

        // Act
        var result = await _service.CreateExperimentAsync(experiment, "testuser");

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Item.ContainsKey("GSI1PK"));
        Assert.Equal("EXPERIMENT", capturedRequest.Item["GSI1PK"].S);
        Assert.True(capturedRequest.Item.ContainsKey("GSI1SK"));
        Assert.NotEmpty(capturedRequest.Item["GSI1SK"].S);

        // GSI1SK should be an ISO-8601 timestamp (parseable)
        Assert.True(DateTime.TryParse(capturedRequest.Item["GSI1SK"].S, out _));

        Assert.NotNull(result);
        Assert.IsType<IdResponseDto>(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Id));
    }

    [Fact]
    public async Task CreateExperimentAsync_ShouldSetTopLevelStatusDraft()
    {
        // Arrange
        var experiment = new Experiment
        {
            Data = new ExperimentData { Name = "Test Experiment" },
            QuestionnaireConfig = new QuestionnaireConfig()
        };

        PutItemRequest? capturedRequest = null;
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .Callback<PutItemRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new PutItemResponse());

        // Act
        await _service.CreateExperimentAsync(experiment, "testuser");

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Item.ContainsKey("status"));
        Assert.Equal("Draft", capturedRequest.Item["status"].S);
    }



    [Fact(Skip = "Refactor: moved to Session/Membership services")]
    public async Task SyncExperimentAsync_ShouldReturnCorrectFormat()
    {
        // Quarantined - moved to LegacyMonolith/session services
        await Task.CompletedTask;
    }

    [Fact(Skip = "Refactor: moved to Session/Membership services")]
    public async Task SyncExperimentAsync_ShouldValidateExperimentExists()
    {
        // Quarantined - moved to LegacyMonolith/session services
        await Task.CompletedTask;
    }

    [Fact(Skip = "Refactor: moved to Session/Membership services")]
    public async Task SyncExperimentAsync_ShouldUseCorrectQueryFormat()
    {
        // Quarantined - moved to LegacyMonolith/session services
        await Task.CompletedTask;
    }

    [Fact(Skip = "Refactor: moved to Session/Membership services")]
    public async Task AddMemberAsync_ShouldCreateCorrectMemberPKFormat()
    {
        // Quarantined - moved to LegacyMonolith/membership services
        await Task.CompletedTask;
    }

    [Fact(Skip = "Refactor: moved to Session/Membership services")]
    public async Task AddMemberAsync_ShouldCreateCorrectMemberSKFormat()
    {
        // Quarantined - moved to LegacyMonolith/membership services
        await Task.CompletedTask;
    }

    [Fact(Skip = "Refactor: moved to Session/Membership services")]
    public async Task AddMemberAsync_ShouldSetCorrectMemberType()
    {
        // Quarantined - moved to LegacyMonolith/membership services
        await Task.CompletedTask;
    }

    [Fact(Skip = "Refactor: moved to Session/Membership services")]
    public async Task AddMemberAsync_ShouldIncludeMemberAuditFields()
    {
        // Quarantined - moved to LegacyMonolith/membership services
        await Task.CompletedTask;
    }

    [Fact]
    public async Task UpdateExperimentAsync_ShouldUseCorrectKeyFormat()
    {
        // Arrange
        var data = new ExperimentData { Name = "Updated Experiment" };

        UpdateItemRequest? capturedRequest = null;
        _mockDynamoClient.Setup(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .Callback<UpdateItemRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new UpdateItemResponse());

        // Act
        await _service.UpdateExperimentAsync("test-id", data, "testuser");

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Key.ContainsKey("PK"));
        Assert.Equal("EXPERIMENT#test-id", capturedRequest.Key["PK"].S);
        Assert.True(capturedRequest.Key.ContainsKey("SK"));
        Assert.Equal("METADATA", capturedRequest.Key["SK"].S);
    }

    [Fact]
    public async Task DeleteExperimentAsync_ShouldUseCorrectKeyFormat()
    {
        // Arrange
        DeleteItemRequest? capturedRequest = null;
        _mockDynamoClient.Setup(x => x.DeleteItemAsync(It.IsAny<DeleteItemRequest>(), default))
            .Callback<DeleteItemRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new DeleteItemResponse());

        // Act
        await _service.DeleteExperimentAsync("test-id", "testuser");

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Key.ContainsKey("PK"));
        Assert.Equal("EXPERIMENT#test-id", capturedRequest.Key["PK"].S);
        Assert.True(capturedRequest.Key.ContainsKey("SK"));
        Assert.Equal("METADATA", capturedRequest.Key["SK"].S);
    }

    [Fact]
    public async Task ActivateExperimentAsync_ShouldUpdateTopLevelStatusWithStrictCondition()
    {
        // Arrange
        UpdateItemRequest? capturedRequest = null;
        _mockDynamoClient.Setup(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .Callback<UpdateItemRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new UpdateItemResponse());

        // Act
        await _service.ActivateExperimentAsync("test-id", "testuser");

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal("SET #status = :to, updatedBy = :u, updatedAt = :t", capturedRequest.UpdateExpression);
        Assert.Contains("#status = :from0", capturedRequest.ConditionExpression);
        Assert.Contains("#status = :from1", capturedRequest.ConditionExpression);
        Assert.Equal("Active", capturedRequest.ExpressionAttributeValues[":to"].S);
    }

    [Fact]
    public async Task PauseExperimentAsync_ShouldUpdateTopLevelStatusWithStrictCondition()
    {
        // Arrange
        UpdateItemRequest? capturedRequest = null;
        _mockDynamoClient.Setup(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .Callback<UpdateItemRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new UpdateItemResponse());

        // Act
        await _service.PauseExperimentAsync("test-id", "testuser");

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal("SET #status = :to, updatedBy = :u, updatedAt = :t", capturedRequest.UpdateExpression);
        Assert.Contains("#status = :from0", capturedRequest.ConditionExpression);
        Assert.Equal("Paused", capturedRequest.ExpressionAttributeValues[":to"].S);
    }

    [Fact]
    public async Task CloseExperimentAsync_ShouldUpdateTopLevelStatusWithStrictCondition()
    {
        // Arrange
        UpdateItemRequest? capturedRequest = null;
        _mockDynamoClient.Setup(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .Callback<UpdateItemRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new UpdateItemResponse());

        // Act
        await _service.CloseExperimentAsync("test-id", "testuser");

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal("SET #status = :to, updatedBy = :u, updatedAt = :t", capturedRequest.UpdateExpression);
        Assert.Contains("#status = :from0", capturedRequest.ConditionExpression);
        Assert.Contains("#status = :from1", capturedRequest.ConditionExpression);
        Assert.Equal("Closed", capturedRequest.ExpressionAttributeValues[":to"].S);
    }
}
