using Xunit;
using Moq;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGExperimentsLambda.Services;
using AI4NGExperimentsLambda.Models;

namespace AI4NGExperiments.Tests;

public class ReferentialIntegrityTests
{
    private readonly Mock<IAmazonDynamoDB> _mockDynamoClient;
    private readonly ExperimentService _service;

    public ReferentialIntegrityTests()
    {
        _mockDynamoClient = new Mock<IAmazonDynamoDB>();
        Environment.SetEnvironmentVariable("EXPERIMENTS_TABLE", "experiments-test");
        Environment.SetEnvironmentVariable("RESPONSES_TABLE", "responses-test");
        Environment.SetEnvironmentVariable("QUESTIONNAIRES_TABLE", "questionnaires-test");
        _service = new ExperimentService(_mockDynamoClient.Object);
    }

    [Fact]
    public async Task CreateExperimentAsync_ShouldValidateQuestionnaireExists()
    {
        // Arrange
        var experiment = new Experiment
        {
            Data = new ExperimentData { Name = "Test Experiment" },
            QuestionnaireConfig = new QuestionnaireConfig 
            { 
                QuestionnaireIds = new List<string> { "non-existent-questionnaire" }
            }
        };

        // Mock questionnaire not found
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse { Item = null });

        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ReturnsAsync(new PutItemResponse());

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
            Data = new ExperimentData { Name = "Test Experiment" },
            QuestionnaireConfig = new QuestionnaireConfig 
            { 
                QuestionnaireIds = new List<string> { "questionnaire-1", "questionnaire-2" }
            }
        };

        // Mock questionnaires exist
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse 
            { 
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
        _mockDynamoClient.Verify(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task SyncExperimentAsync_ShouldValidateExperimentExists()
    {
        // Arrange
        var syncData = new SyncRequest
        {
            Sessions = new List<Session>
            {
                new() { SessionId = "session-1", ParticipantId = "participant-1", Status = "active" }
            }
        };

        // Mock experiment not found
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse { Item = null });

        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ReturnsAsync(new PutItemResponse());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SyncExperimentAsync("non-existent-experiment", syncData, "testuser"));
        
        Assert.Contains("experiment", exception.Message.ToLower());
        Assert.Contains("not found", exception.Message.ToLower());
    }

    [Fact]
    public async Task SyncExperimentAsync_ShouldValidateUniqueSessionIds()
    {
        // Arrange
        var syncData = new SyncRequest
        {
            Sessions = new List<Session>
            {
                new() { SessionId = "session-1", ParticipantId = "participant-1", Status = "active" },
                new() { SessionId = "session-1", ParticipantId = "participant-2", Status = "active" } // Duplicate
            }
        };

        // Mock experiment exists
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse 
            { 
                Item = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue("EXPERIMENT#test-experiment")
                }
            });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SyncExperimentAsync("test-experiment", syncData, "testuser"));
        
        Assert.Contains("session", exception.Message.ToLower());
        Assert.Contains("unique", exception.Message.ToLower());
    }
}