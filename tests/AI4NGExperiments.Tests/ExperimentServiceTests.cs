using Xunit;
using Moq;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGExperimentsLambda.Services;
using AI4NGExperimentsLambda.Models;

namespace AI4NGExperiments.Tests;

public class ExperimentServiceTests
{
    private readonly Mock<IAmazonDynamoDB> _mockDynamoClient;
    private readonly ExperimentService _service;

    public ExperimentServiceTests()
    {
        _mockDynamoClient = new Mock<IAmazonDynamoDB>();
        Environment.SetEnvironmentVariable("EXPERIMENTS_TABLE", "experiments-test");
        Environment.SetEnvironmentVariable("RESPONSES_TABLE", "responses-test");
        Environment.SetEnvironmentVariable("QUESTIONNAIRES_TABLE", "questionnaires-test");

        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse { IsItemSet = false });
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ReturnsAsync(new PutItemResponse());

        _service = new ExperimentService(_mockDynamoClient.Object);
    }

    [Fact]
    public async Task CreateExperimentAsync_ShouldReturnId_WhenValid()
    {
        // Arrange
        var experiment = new Experiment
        {
            Data = new ExperimentData 
            { 
                Name = "Test Experiment",
                SessionTypes = new Dictionary<string, SessionType>
                {
                    ["DAILY"] = new SessionType
                    {
                        Name = "Daily Session",
                        Questionnaires = new List<string> { "PQ" },
                        Tasks = new List<string> { "TRAIN_EEG" }
                    }
                }
            },
            QuestionnaireConfig = new QuestionnaireConfig 
            { 
                Schedule = new Dictionary<string, string> { ["PQ"] = "every_session" }
            }
        };

        // Mock questionnaire exists
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.Is<GetItemRequest>(r => 
            r.Key["PK"].S == "QUESTIONNAIRE#PQ"), default))
            .ReturnsAsync(new GetItemResponse { IsItemSet = true });

        // Act
        var result = await _service.CreateExperimentAsync(experiment, "testuser");

        // Assert
        Assert.NotNull(result);
        _mockDynamoClient.Verify(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task GetExperimentsAsync_ShouldReturnExperiments()
    {
        // Arrange
        var scanResponse = new ScanResponse
        {
            Items = new List<Dictionary<string, AttributeValue>>
            {
                new()
                {
                    ["PK"] = new AttributeValue("EXPERIMENT#test-id"),
                    ["data"] = new AttributeValue
                    {
                        M = new Dictionary<string, AttributeValue>
                        {
                            ["name"] = new AttributeValue("Test Experiment"),
                            ["description"] = new AttributeValue("Test Description")
                        }
                    }
                }
            }
        };

        _mockDynamoClient.Setup(x => x.ScanAsync(It.IsAny<ScanRequest>(), default))
            .ReturnsAsync(scanResponse);

        // Act
        var result = await _service.GetExperimentsAsync();

        // Assert
        Assert.Single(result);
        _mockDynamoClient.Verify(x => x.ScanAsync(It.IsAny<ScanRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task GetExperimentAsync_ShouldReturnExperiment_WhenExists()
    {
        // Arrange
        var queryResponse = new QueryResponse
        {
            Items = new List<Dictionary<string, AttributeValue>>
            {
                new()
                {
                    ["PK"] = new AttributeValue("EXPERIMENT#test-id"),
                    ["SK"] = new AttributeValue("METADATA"),
                    ["data"] = new AttributeValue { M = new Dictionary<string, AttributeValue> { ["name"] = new AttributeValue("Test") } },
                    ["questionnaireConfig"] = new AttributeValue { M = new Dictionary<string, AttributeValue>() }
                },
                new()
                {
                    ["PK"] = new AttributeValue("EXPERIMENT#test-id"),
                    ["SK"] = new AttributeValue("SESSION#session-1"),
                    ["data"] = new AttributeValue { M = new Dictionary<string, AttributeValue> { ["sessionId"] = new AttributeValue("session-1") } }
                }
            }
        };

        _mockDynamoClient.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), default))
            .ReturnsAsync(queryResponse);

        // Act
        var result = await _service.GetExperimentAsync("test-id");

        // Assert
        Assert.NotNull(result);
        _mockDynamoClient.Verify(x => x.QueryAsync(It.IsAny<QueryRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task GetExperimentAsync_ShouldReturnNull_WhenNotExists()
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), default))
            .ReturnsAsync(new QueryResponse { Items = new List<Dictionary<string, AttributeValue>>() });

        // Act
        var result = await _service.GetExperimentAsync("non-existent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetMyExperimentsAsync_ShouldReturnUserExperiments()
    {
        // Arrange
        var queryResponse = new QueryResponse
        {
            Items = new List<Dictionary<string, AttributeValue>>
            {
                new()
                {
                    ["PK"] = new AttributeValue("EXPERIMENT#test-id"),
                    ["data"] = new AttributeValue
                    {
                        M = new Dictionary<string, AttributeValue>
                        {
                            ["Name"] = new AttributeValue("My Experiment")
                        }
                    },
                    ["role"] = new AttributeValue("researcher")
                }
            }
        };

        _mockDynamoClient.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), default))
            .ReturnsAsync(queryResponse);

        // Act
        var result = await _service.GetMyExperimentsAsync("testuser");

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public async Task UpdateExperimentAsync_ShouldCallUpdateItem()
    {
        // Arrange
        var data = new ExperimentData { Name = "Updated Experiment" };
        _mockDynamoClient.Setup(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .ReturnsAsync(new UpdateItemResponse());

        // Act
        await _service.UpdateExperimentAsync("test-id", data, "testuser");

        // Assert
        _mockDynamoClient.Verify(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task DeleteExperimentAsync_ShouldCallDeleteItem()
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.DeleteItemAsync(It.IsAny<DeleteItemRequest>(), default))
            .ReturnsAsync(new DeleteItemResponse());

        // Act
        await _service.DeleteExperimentAsync("test-id", "testuser");

        // Assert
        _mockDynamoClient.Verify(x => x.DeleteItemAsync(It.IsAny<DeleteItemRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task SyncExperimentAsync_ShouldReturnChanges_WhenValid()
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
                    new()
                    {
                        ["PK"] = new AttributeValue("EXPERIMENT#test-id"),
                        ["SK"] = new AttributeValue("METADATA"),
                        ["data"] = new AttributeValue { M = new Dictionary<string, AttributeValue> { ["name"] = new AttributeValue("Test") } },
                        ["updatedAt"] = new AttributeValue(DateTime.UtcNow.ToString("O"))
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
    public async Task GetExperimentMembersAsync_ShouldReturnMembers()
    {
        // Arrange
        var queryResponse = new QueryResponse
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
        };

        _mockDynamoClient.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), default))
            .ReturnsAsync(queryResponse);

        // Act
        var result = await _service.GetExperimentMembersAsync("test-id");

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public async Task AddMemberAsync_ShouldAddMember_WhenValid()
    {
        // Arrange
        var memberData = new MemberRequest { Role = "participant" };

        // Act
        await _service.AddMemberAsync("test-id", "user123", memberData, "testuser");

        // Assert
        _mockDynamoClient.Verify(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task RemoveMemberAsync_ShouldRemoveMember_WhenExists()
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.DeleteItemAsync(It.IsAny<DeleteItemRequest>(), default))
            .ReturnsAsync(new DeleteItemResponse());

        // Act
        await _service.RemoveMemberAsync("test-id", "user123", "testuser");

        // Assert
        _mockDynamoClient.Verify(x => x.DeleteItemAsync(It.IsAny<DeleteItemRequest>(), default), Times.Once);
    }
}