using Xunit;
using Moq;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGExperimentsLambda.Services;
using AI4NGExperimentsLambda.Models;

namespace AI4NGExperiments.Tests;

public class DynamoDBFormatTests
{
    private readonly Mock<IAmazonDynamoDB> _mockDynamoClient;
    private readonly ExperimentService _service;

    public DynamoDBFormatTests()
    {
        _mockDynamoClient = new Mock<IAmazonDynamoDB>();
        Environment.SetEnvironmentVariable("EXPERIMENTS_TABLE", "experiments-test");
        Environment.SetEnvironmentVariable("RESPONSES_TABLE", "responses-test");
        Environment.SetEnvironmentVariable("QUESTIONNAIRES_TABLE", "questionnaires-test");
        
        _service = new ExperimentService(_mockDynamoClient.Object);
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
    public async Task SyncExperimentAsync_ShouldReturnCorrectFormat()
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
                        ["SK"] = new AttributeValue("METADATA"),
                        ["data"] = new AttributeValue { M = new Dictionary<string, AttributeValue> { ["name"] = new AttributeValue("Test") } },
                        ["questionnaireConfig"] = new AttributeValue { M = new Dictionary<string, AttributeValue>() }
                    }
                }
            });

        // Act
        var result = await _service.SyncExperimentAsync("test-id", lastSyncTime, "testuser");

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task SyncExperimentAsync_ShouldValidateExperimentExists()
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse { Item = null });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _service.SyncExperimentAsync("test-id", null, "testuser"));
    }

    [Fact]
    public async Task SyncExperimentAsync_ShouldUseCorrectQueryFormat()
    {
        // Arrange
        var lastSyncTime = DateTime.UtcNow.AddHours(-1);

        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse 
            { 
                Item = new Dictionary<string, AttributeValue> { ["PK"] = new AttributeValue("EXPERIMENT#test-id") }
            });

        QueryRequest? capturedRequest = null;
        _mockDynamoClient.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), default))
            .Callback<QueryRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new QueryResponse { Items = new List<Dictionary<string, AttributeValue>>() });

        // Act
        await _service.SyncExperimentAsync("test-id", lastSyncTime, "testuser");

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Contains("PK = :pk", capturedRequest.KeyConditionExpression);
        Assert.True(capturedRequest.ExpressionAttributeValues.ContainsKey(":pk"));
        Assert.Equal("EXPERIMENT#test-id", capturedRequest.ExpressionAttributeValues[":pk"].S);
    }

    [Fact]
    public async Task AddMemberAsync_ShouldCreateCorrectMemberPKFormat()
    {
        // Arrange
        var memberData = new MemberRequest { Role = "participant" };

        PutItemRequest? capturedRequest = null;
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .Callback<PutItemRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new PutItemResponse());

        // Act
        await _service.AddMemberAsync("test-id", "user123", memberData, "testuser");

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Item.ContainsKey("PK"));
        Assert.Equal("EXPERIMENT#test-id", capturedRequest.Item["PK"].S);
    }

    [Fact]
    public async Task AddMemberAsync_ShouldCreateCorrectMemberSKFormat()
    {
        // Arrange
        var memberData = new MemberRequest { Role = "participant" };

        PutItemRequest? capturedRequest = null;
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .Callback<PutItemRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new PutItemResponse());

        // Act
        await _service.AddMemberAsync("test-id", "user123", memberData, "testuser");

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Item.ContainsKey("SK"));
        Assert.Equal("MEMBER#user123", capturedRequest.Item["SK"].S);
    }

    [Fact]
    public async Task AddMemberAsync_ShouldSetCorrectMemberType()
    {
        // Arrange
        var memberData = new MemberRequest { Role = "participant" };

        PutItemRequest? capturedRequest = null;
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .Callback<PutItemRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new PutItemResponse());

        // Act
        await _service.AddMemberAsync("test-id", "user123", memberData, "testuser");

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Item.ContainsKey("type"));
        Assert.Equal("Member", capturedRequest.Item["type"].S);
    }

    [Fact]
    public async Task AddMemberAsync_ShouldIncludeMemberAuditFields()
    {
        // Arrange
        var memberData = new MemberRequest { Role = "participant" };

        PutItemRequest? capturedRequest = null;
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .Callback<PutItemRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new PutItemResponse());

        // Act
        await _service.AddMemberAsync("test-id", "user123", memberData, "testuser");

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Item.ContainsKey("role"));
        Assert.Equal("participant", capturedRequest.Item["role"].S);
        Assert.True(capturedRequest.Item.ContainsKey("addedBy"));
        Assert.Equal("testuser", capturedRequest.Item["addedBy"].S);
        Assert.True(capturedRequest.Item.ContainsKey("addedAt"));
        Assert.NotEmpty(capturedRequest.Item["addedAt"].S);
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
}