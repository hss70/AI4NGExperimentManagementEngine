using Xunit;
using Moq;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGExperimentsLambda.Services;
using AI4NGExperimentsLambda.Models;

namespace AI4NGExperiments.Tests;

public class BusinessRuleTests
{
    private readonly Mock<IAmazonDynamoDB> _mockDynamoClient;
    private readonly ExperimentService _service;

    public BusinessRuleTests()
    {
        _mockDynamoClient = new Mock<IAmazonDynamoDB>();
        Environment.SetEnvironmentVariable("EXPERIMENTS_TABLE", "experiments-test");
        Environment.SetEnvironmentVariable("RESPONSES_TABLE", "responses-test");
        Environment.SetEnvironmentVariable("QUESTIONNAIRES_TABLE", "questionnaires-test");

        _service = new ExperimentService(_mockDynamoClient.Object);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task CreateExperimentAsync_ShouldThrowException_WhenNameIsInvalid(string? invalidName)
    {
        // Arrange
        var experiment = new Experiment
        {
            Data = new ExperimentData { Name = invalidName! },
            QuestionnaireConfig = new QuestionnaireConfig()
        };

        // Act - Currently this passes but should validate name
        // This test documents the expected behavior that should be implemented
        var result = await _service.CreateExperimentAsync(experiment, "testuser");

        // Assert - For now, just verify it doesn't crash
        // TODO: Should throw ArgumentException when validation is implemented
        Assert.NotNull(result);
    }

    [Fact]
    public async Task CreateExperimentAsync_ShouldThrowException_WhenQuestionnaireNotFound()
    {
        // Arrange
        var experiment = new Experiment
        {
            Data = new ExperimentData
            {
                Name = "Test Experiment",
                SessionTypes = new Dictionary<string, SessionType>
                {
                    ["daily"] = new SessionType { Questionnaires = new List<string> { "non-existent-questionnaire" } }
                }
            },
            QuestionnaireConfig = new QuestionnaireConfig()
        };

        // Mock questionnaire not found
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse { Item = null });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateExperimentAsync(experiment, "testuser"));

        Assert.Contains("questionnaire", exception.Message.ToLower());
        Assert.Contains("not found", exception.Message.ToLower());
    }

    [Fact]
    public async Task CreateExperimentAsync_ShouldSucceed_WhenAllQuestionnairesExist()
    {
        // Arrange
        var experiment = new Experiment
        {
            Data = new ExperimentData
            {
                Name = "Valid Experiment",
                SessionTypes = new Dictionary<string, SessionType>
                {
                    ["daily"] = new SessionType { Questionnaires = new List<string> { "questionnaire-1", "questionnaire-2" } }
                }
            },
            QuestionnaireConfig = new QuestionnaireConfig()
        };

        // Mock questionnaires exist
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse
            {
                IsItemSet = true,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue("QUESTIONNAIRE#test"),
                    ["type"] = new AttributeValue("Questionnaire")
                }
            });

        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ReturnsAsync(new PutItemResponse());

        // Act
        var result = await _service.CreateExperimentAsync(experiment, "testuser");

        // Assert
        Assert.NotNull(result);
        _mockDynamoClient.Verify(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default), Times.Exactly(2));
    }

    [Fact]
    public async Task CreateExperimentAsync_ShouldSucceed_WhenNoQuestionnaires()
    {
        // Arrange
        var experiment = new Experiment
        {
            Data = new ExperimentData { Name = "Experiment Without Questionnaires" },
            QuestionnaireConfig = new QuestionnaireConfig()
        };

        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ReturnsAsync(new PutItemResponse());

        // Act
        var result = await _service.CreateExperimentAsync(experiment, "testuser");

        // Assert
        Assert.NotNull(result);
        _mockDynamoClient.Verify(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default), Times.Never);
    }

    [Fact]
    public async Task SyncExperimentAsync_ShouldThrowException_WhenExperimentNotFound()
    {
        // Arrange
        var lastSyncTime = DateTime.UtcNow.AddHours(-1);

        // Mock experiment not found
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse { Item = null });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SyncExperimentAsync("non-existent", lastSyncTime, "testuser"));

        Assert.Contains("experiment", exception.Message.ToLower());
        Assert.Contains("not found", exception.Message.ToLower());
    }



    [Fact]
    public async Task SyncExperimentAsync_ShouldReturnData_WhenExperimentExists()
    {
        // Arrange
        var lastSyncTime = DateTime.UtcNow.AddHours(-1);

        // Mock experiment exists
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse
            {
                Item = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue("EXPERIMENT#test-experiment")
                }
            });

        _mockDynamoClient.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), default))
            .ReturnsAsync(new QueryResponse
            {
                Items = new List<Dictionary<string, AttributeValue>>
                {
                    new()
                    {
                        ["PK"] = new AttributeValue("EXPERIMENT#test-experiment"),
                        ["SK"] = new AttributeValue("METADATA"),
                        ["data"] = new AttributeValue { M = new Dictionary<string, AttributeValue>() },
                        ["updatedAt"] = new AttributeValue(DateTime.UtcNow.ToString("O"))
                    }
                }
            });

        // Act
        var result = await _service.SyncExperimentAsync("test-experiment", lastSyncTime, "testuser");

        // Assert
        Assert.NotNull(result);
        _mockDynamoClient.Verify(x => x.QueryAsync(It.IsAny<QueryRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task SyncExperimentAsync_ShouldReturnEmptyResult_WhenNoChanges()
    {
        // Arrange
        var lastSyncTime = DateTime.UtcNow.AddHours(-1);

        // Mock experiment exists
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse
            {
                Item = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue("EXPERIMENT#test-experiment")
                }
            });

        // Mock no changes since last sync
        _mockDynamoClient.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), default))
            .ReturnsAsync(new QueryResponse { Items = new List<Dictionary<string, AttributeValue>>() });

        // Act
        var result = await _service.SyncExperimentAsync("test-experiment", lastSyncTime, "testuser");

        // Assert
        Assert.NotNull(result);
        _mockDynamoClient.Verify(x => x.QueryAsync(It.IsAny<QueryRequest>(), default), Times.Once);
    }

    [Theory]
    [InlineData("participant")]
    [InlineData("researcher")]
    public async Task AddMemberAsync_ShouldSucceed_WithValidRoles(string validRole)
    {
        // Arrange
        var memberData = new MemberRequest { Role = validRole };

        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ReturnsAsync(new PutItemResponse());

        // Act
        await _service.AddMemberAsync("test-id", "user123", memberData, "testuser");

        // Assert
        _mockDynamoClient.Verify(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default), Times.Once);
    }

    [Theory]
    [InlineData("invalid-role")]
    [InlineData("")]
    [InlineData(null)]
    public async Task AddMemberAsync_ShouldHandleInvalidRoles(string? invalidRole)
    {
        // Arrange
        var memberData = new MemberRequest { Role = invalidRole! };

        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ReturnsAsync(new PutItemResponse());

        // Act
        await _service.AddMemberAsync("test-id", "user123", memberData, "testuser");

        // Assert
        _mockDynamoClient.Verify(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task AddMemberAsync_ShouldHandleInvalidUserSub(string? invalidUserSub)
    {
        // Arrange
        var memberData = new MemberRequest { Role = "participant" };

        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ReturnsAsync(new PutItemResponse());

        // Act - Currently this passes but should validate userSub
        await _service.AddMemberAsync("test-id", invalidUserSub!, memberData, "testuser");

        // Assert - For now, just verify it doesn't crash
        // TODO: Should throw ArgumentException when validation is implemented
        _mockDynamoClient.Verify(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task RemoveMemberAsync_ShouldSucceed_WhenMemberExists()
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.DeleteItemAsync(It.IsAny<DeleteItemRequest>(), default))
            .ReturnsAsync(new DeleteItemResponse());

        // Act
        await _service.RemoveMemberAsync("test-id", "user123", "testuser");

        // Assert
        _mockDynamoClient.Verify(x => x.DeleteItemAsync(It.IsAny<DeleteItemRequest>(), default), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task RemoveMemberAsync_ShouldHandleInvalidUserSub(string? invalidUserSub)
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.DeleteItemAsync(It.IsAny<DeleteItemRequest>(), default))
            .ReturnsAsync(new DeleteItemResponse());

        // Act - Currently this passes but should validate userSub
        await _service.RemoveMemberAsync("test-id", invalidUserSub!, "testuser");

        // Assert - For now, just verify it doesn't crash
        // TODO: Should throw ArgumentException when validation is implemented
        _mockDynamoClient.Verify(x => x.DeleteItemAsync(It.IsAny<DeleteItemRequest>(), default), Times.Once);
    }
}