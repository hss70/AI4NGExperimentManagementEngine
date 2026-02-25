using Moq;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGExperimentsLambda.Services;

using AI4NGExperimentsLambda.Models;

namespace AI4NGExperiments.Tests;

public class ReferentialIntegrityTests
{
    private readonly Mock<IAmazonDynamoDB> _mockDynamoClient;
    private readonly ExperimentsService _service;

    public ReferentialIntegrityTests()
    {
        _mockDynamoClient = new Mock<IAmazonDynamoDB>();
        Environment.SetEnvironmentVariable("EXPERIMENTS_TABLE", "experiments-test");
        Environment.SetEnvironmentVariable("RESPONSES_TABLE", "responses-test");
        Environment.SetEnvironmentVariable("QUESTIONNAIRES_TABLE", "questionnaires-test");
        _service = new ExperimentsService(_mockDynamoClient.Object);
    }

    [Fact]
    public async Task CreateExperimentAsync_ShouldValidateQuestionnaireExists()
    {
        // Arrange
        var missingQuestionnaireName = "non-existent-questionnaire";
        var experiment = new Experiment
        {
            Data = new ExperimentData
            {
                Name = "Test Experiment",
                SessionTypes = new Dictionary<string, SessionType>
                {
                    ["daily"] = new SessionType { Questionnaires = new List<string> { missingQuestionnaireName } }
                }
            },
            QuestionnaireConfig = new QuestionnaireConfig()
        };

        // Mock questionnaire not found
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse { Item = null });

        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ReturnsAsync(new PutItemResponse());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateExperimentAsync(experiment, "testuser"));

        Assert.Contains("missing questionnaires", exception.Message.ToLower());
        Assert.Contains(missingQuestionnaireName, exception.Message.ToLower());
    }

    [Fact]
    public async Task CreateExperimentAsync_ShouldSucceed_WhenAllQuestionnairesExist()
    {
        // Arrange
        var experiment = new Experiment
        {
            Data = new ExperimentData
            {
                Name = "Test Experiment",
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

    [Fact(Skip = "Refactor: moved to Session/Membership services")]
    public async Task SyncExperimentAsync_ShouldValidateExperimentExists()
    {
        // Quarantined - moved to LegacyMonolith/session services
        await Task.CompletedTask;
    }
}