using Moq;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGExperimentsLambda.Services;

using AI4NGExperimentsLambda.Models;
using AI4NGExperimentsLambda.Models.Dtos;

namespace AI4NGExperiments.Tests;

public class ExperimentServiceTests
{
    private readonly Mock<IAmazonDynamoDB> _mockDynamoClient;
    private readonly ExperimentsService _service;

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

        _service = new ExperimentsService(_mockDynamoClient.Object);
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
        Assert.IsType<IdResponseDto>(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Id));
        _mockDynamoClient.Verify(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task GetExperimentsAsync_ShouldReturnExperiments()
    {
        // Arrange - service now queries GSI1
        var queryResponse = new QueryResponse
        {
            Items = new List<Dictionary<string, AttributeValue>>
            {
                new()
                {
                    ["PK"] = new AttributeValue("EXPERIMENT#test-id"),
                    ["status"] = new AttributeValue("Active"),
                    ["data"] = new AttributeValue
                    {
                        M = new Dictionary<string, AttributeValue>
                        {
                            ["Name"] = new AttributeValue("Test Experiment"),
                            ["Description"] = new AttributeValue("Test Description")
                        }
                    }
                }
            }
        };

        QueryRequest? capturedQuery = null;
        _mockDynamoClient.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), default))
            .Callback<QueryRequest, CancellationToken>((req, _) => capturedQuery = req)
            .ReturnsAsync(queryResponse);

        // Provide ScanAsync handler so Verify can ensure it was not called
        _mockDynamoClient.Setup(x => x.ScanAsync(It.IsAny<ScanRequest>(), default))
            .ReturnsAsync(new ScanResponse());

        // Act
        var result = await _service.GetExperimentsAsync();

        // Assert
        Assert.Single(result);
        _mockDynamoClient.Verify(x => x.QueryAsync(It.IsAny<QueryRequest>(), default), Times.Once);
        _mockDynamoClient.Verify(x => x.ScanAsync(It.IsAny<ScanRequest>(), default), Times.Never);
        Assert.NotNull(capturedQuery);
        Assert.Equal("GSI1", capturedQuery.IndexName);
        Assert.Contains("GSI1PK", capturedQuery.KeyConditionExpression);
        Assert.Contains("#status", capturedQuery.ProjectionExpression);
        Assert.Equal("Active", result[0].Status);
    }

    [Fact]
    public async Task GetExperimentAsync_ShouldReturnExperiment_WhenExists()
    {
        // Arrange - metadata is fetched via GetItemAsync
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = "EXPERIMENT#test-id" },
            ["SK"] = new AttributeValue { S = "METADATA" },
            ["status"] = new AttributeValue { S = "Paused" },
            ["data"] = new AttributeValue { M = new Dictionary<string, AttributeValue> { ["Name"] = new AttributeValue("Test") } },
            ["questionnaireConfig"] = new AttributeValue { M = new Dictionary<string, AttributeValue>() }
        };

        GetItemRequest? capturedGet = null;
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .Callback<GetItemRequest, CancellationToken>((req, _) => capturedGet = req)
            .ReturnsAsync(new GetItemResponse { IsItemSet = true, Item = item });

        // Provide QueryAsync handler so we can assert it was NOT used
        _mockDynamoClient.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), default))
            .ReturnsAsync(new QueryResponse());

        // Act
        var result = await _service.GetExperimentAsync("test-id");

        // Assert
        Assert.NotNull(result);
        _mockDynamoClient.Verify(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default), Times.Once);
        _mockDynamoClient.Verify(x => x.QueryAsync(It.IsAny<QueryRequest>(), default), Times.Never);
        Assert.NotNull(capturedGet);
        Assert.True(capturedGet.Key.ContainsKey("PK"));
        Assert.Equal("EXPERIMENT#test-id", capturedGet.Key["PK"].S);
        Assert.True(capturedGet.Key.ContainsKey("SK"));
        Assert.Equal("METADATA", capturedGet.Key["SK"].S);
        Assert.Equal("Paused", result.Status);
    }

    [Fact]
    public async Task GetExperimentAsync_ShouldReturnNull_WhenNotExists()
    {
        // Arrange - GetItemAsync returns no item
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse { IsItemSet = false, Item = null });

        // Act
        var result = await _service.GetExperimentAsync("non-existent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetExperimentAsync_ShouldFallbackStatus_FromLegacyDataStatus_WhenTopLevelMissing()
    {
        // Arrange
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = "EXPERIMENT#test-id" },
            ["SK"] = new AttributeValue { S = "METADATA" },
            ["data"] = new AttributeValue
            {
                M = new Dictionary<string, AttributeValue>
                {
                    ["Name"] = new AttributeValue("Test"),
                    ["Status"] = new AttributeValue("Closed")
                }
            },
            ["questionnaireConfig"] = new AttributeValue { M = new Dictionary<string, AttributeValue>() }
        };

        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse { IsItemSet = true, Item = item });

        // Act
        var result = await _service.GetExperimentAsync("test-id");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Closed", result.Status);
    }

    [Fact]
    public async Task GetExperimentsAsync_ShouldFallbackStatus_FromLegacyDataStatus_WhenTopLevelMissing()
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
                            ["Name"] = new AttributeValue("Test Experiment"),
                            ["Description"] = new AttributeValue("Test Description"),
                            ["Status"] = new AttributeValue("Draft")
                        }
                    }
                }
            }
        };

        _mockDynamoClient.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), default))
            .ReturnsAsync(queryResponse);

        // Act
        var result = await _service.GetExperimentsAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("Draft", result[0].Status);
    }

    [Fact]
    public async Task ValidateExperimentAsync_ShouldReturnValidateDto_WhenQuestionnairesExist()
    {
        // Arrange - prepare an experiment that references questionnaire PQ
        var experiment = new Experiment
        {
            Data = new ExperimentData(),
            QuestionnaireConfig = new QuestionnaireConfig
            {
                Schedule = new Dictionary<string, string> { ["PQ"] = "every_session" }
            }
        };

        // Mock questionnaire exists
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.Is<GetItemRequest>(r => r.Key["PK"].S == "QUESTIONNAIRE#PQ"), default))
            .ReturnsAsync(new GetItemResponse { IsItemSet = true });

        // Act
        var result = await _service.ValidateExperimentAsync(experiment);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<ValidateExperimentResponseDto>(result);
        Assert.True(result.Valid);
        Assert.Contains("PQ", result.ReferencedQuestionnaires);
    }

    [Fact(Skip = "Refactor: moved to Session/Membership services")]
    public async Task GetMyExperimentsAsync_ShouldReturnUserExperiments()
    {
        // Quarantined - moved to LegacyMonolith/membership services
        await Task.CompletedTask;
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

    [Fact(Skip = "Refactor: moved to Session/Membership services")]
    public async Task SyncExperimentAsync_ShouldReturnChanges_WhenValid()
    {
        // Quarantined - moved to LegacyMonolith/session services
        await Task.CompletedTask;
    }

    [Fact(Skip = "Refactor: moved to Session/Membership services")]
    public async Task GetExperimentMembersAsync_ShouldReturnMembers()
    {
        // Quarantined - moved to LegacyMonolith/membership services
        await Task.CompletedTask;
    }

    [Fact(Skip = "Refactor: moved to Session/Membership services")]
    public async Task AddMemberAsync_ShouldAddMember_WhenValid()
    {
        // Quarantined - moved to LegacyMonolith/membership services
        await Task.CompletedTask;
    }

    [Fact(Skip = "Refactor: moved to Session/Membership services")]
    public async Task RemoveMemberAsync_ShouldRemoveMember_WhenExists()
    {
        // Quarantined - moved to LegacyMonolith/membership services
        await Task.CompletedTask;
    }
}
