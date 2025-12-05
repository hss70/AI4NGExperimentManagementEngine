using Xunit;
using Moq;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGExperimentsLambda.Services;
using AI4NGExperimentsLambda.Models;

namespace AI4NGExperiments.Tests;

public class SessionTaskTests
{
    private readonly Mock<IAmazonDynamoDB> _mockDynamoClient;
    private readonly ExperimentService _service;

    public SessionTaskTests()
    {
        _mockDynamoClient = new Mock<IAmazonDynamoDB>();
        Environment.SetEnvironmentVariable("EXPERIMENTS_TABLE", "experiments-test");
        Environment.SetEnvironmentVariable("RESPONSES_TABLE", "responses-test");
        Environment.SetEnvironmentVariable("QUESTIONNAIRES_TABLE", "questionnaires-test");

        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ReturnsAsync(new PutItemResponse());

        _service = new ExperimentService(_mockDynamoClient.Object);
    }

    [Fact]
    public async Task CreateSessionAsync_ShouldReturnSessionId_WhenValid()
    {
        // Arrange
        var request = new CreateSessionRequest
        {
            ExperimentId = "test-exp",
            SessionType = "DAILY",
            Date = "2024-01-15"
        };

        _mockDynamoClient.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), default))
            .ReturnsAsync(new QueryResponse 
            { 
                Items = new List<Dictionary<string, AttributeValue>> 
                { 
                    new() 
                    { 
                        ["SK"] = new AttributeValue("METADATA"),
                        ["data"] = new AttributeValue { M = new Dictionary<string, AttributeValue> { ["name"] = new AttributeValue("Test Experiment") } },
                        ["questionnaireConfig"] = new AttributeValue { M = new Dictionary<string, AttributeValue>() }
                    } 
                } 
            });

        // Act
        var result = await _service.CreateSessionAsync("test-exp", request, "testuser");

        // Assert
        Assert.NotNull(result);
        _mockDynamoClient.Verify(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default), Times.Once);
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
                    ["DAILY"] = new SessionType { Questionnaires = new List<string> { "NONEXISTENT" } }
                }
            },
            QuestionnaireConfig = new QuestionnaireConfig()
        };

        // Create a new service instance for this test to avoid mock interference
        var mockClient = new Mock<IAmazonDynamoDB>();
        
        // Mock questionnaire not found
        mockClient.Setup(x => x.GetItemAsync(It.Is<GetItemRequest>(r => 
            r.Key["PK"].S == "QUESTIONNAIRE#NONEXISTENT"), default))
            .ReturnsAsync(new GetItemResponse { IsItemSet = false, Item = null });
            
        var testService = new ExperimentService(mockClient.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            testService.CreateExperimentAsync(experiment, "testuser"));
    }

    [Fact]
    public async Task GetSessionAsync_ShouldReturnSession_WhenExists()
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse
            {
                IsItemSet = true,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["data"] = new AttributeValue { M = new Dictionary<string, AttributeValue> { ["date"] = new AttributeValue("2024-01-15") } },
                    ["taskOrder"] = new AttributeValue { L = new List<AttributeValue>() },
                    ["createdAt"] = new AttributeValue(DateTime.UtcNow.ToString("O")),
                    ["updatedAt"] = new AttributeValue(DateTime.UtcNow.ToString("O"))
                }
            });

        // Act
        var result = await _service.GetSessionAsync("exp-1", "session-1");

        // Assert
        Assert.NotNull(result);
    }



    [Fact]
    public async Task CreateExperimentAsync_ShouldHandleComplexBCIExperiment()
    {
        // Arrange - BCI Training Study structure
        var experiment = new Experiment
        {
            Data = new ExperimentData
            {
                Name = "BCI Training Study",
                Description = "A 21-day protocol combining EEG training with daily, weekly, and trait questionnaires.",
                SessionTypes = new Dictionary<string, SessionType>
                {
                    ["DAILY"] = new SessionType
                    {
                        Name = "Daily Session",
                        Questionnaires = new List<string> { "PreState", "PhysicalState", "CurrentState", "EndState", "PQ", "TLX" },
                        Tasks = new List<string> { "TASK#PRE_STATE", "TASK#PHYSICAL_STATE", "TASK#TRAIN_EEG", "TASK#CURRENT_STATE", "TASK#END_STATE", "TASK#PQ", "TASK#TLX" },
                        EstimatedDuration = 25,
                        Schedule = "daily"
                    },
                    ["WEEKLY"] = new SessionType
                    {
                        Name = "Weekly Session",
                        Questionnaires = new List<string> { "IPAQ" },
                        Tasks = new List<string> { "TASK#IPAQ" },
                        EstimatedDuration = 20,
                        Schedule = "weekly"
                    },
                    ["TRAIT"] = new SessionType
                    {
                        Name = "Trait Questionnaire Session",
                        Questionnaires = new List<string> { "VVIQ", "ATI", "FMI", "MIQ-RS", "Edinburgh", "MentalRotation", "16PF5", "IndexLearningStyles" },
                        Tasks = new List<string> { "TASK#TRAIT_BANK" },
                        EstimatedDuration = 115,
                        Schedule = "once"
                    }
                }
            },
            QuestionnaireConfig = new QuestionnaireConfig
            {
                Schedule = new Dictionary<string, string>
                {
                    ["PreState"] = "every_session",
                    ["PhysicalState"] = "every_session",
                    ["CurrentState"] = "every_session",
                    ["EndState"] = "every_session",
                    ["PQ"] = "every_session",
                    ["TLX"] = "every_session",
                    ["IPAQ"] = "weekly",
                    ["VVIQ"] = "once",
                    ["ATI"] = "once",
                    ["FMI"] = "once",
                    ["MIQ-RS"] = "once",
                    ["Edinburgh"] = "once",
                    ["MentalRotation"] = "once",
                    ["16PF5"] = "once",
                    ["IndexLearningStyles"] = "once"
                }
            }
        };

        // Mock all questionnaires exist
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse { IsItemSet = true });

        // Act
        var result = await _service.CreateExperimentAsync(experiment, "testuser");

        // Assert
        Assert.NotNull(result);
        // Should validate 15 unique questionnaires (6 daily + 1 weekly + 8 trait)
        _mockDynamoClient.Verify(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default), Times.Exactly(15));
    }

    [Fact]
    public async Task CreateSessionAsync_ShouldHandleMultipleSessionTypes()
    {
        // Arrange
        var dailyRequest = new CreateSessionRequest
        {
            ExperimentId = "BCI_TRAINING_21D",
            SessionType = "DAILY",
            Date = "2024-01-15"
        };

        var weeklyRequest = new CreateSessionRequest
        {
            ExperimentId = "BCI_TRAINING_21D",
            SessionType = "WEEKLY",
            Date = "2024-01-21"
        };

        var traitRequest = new CreateSessionRequest
        {
            ExperimentId = "BCI_TRAINING_21D",
            SessionType = "TRAIT",
            Date = "2024-01-01"
        };

        _mockDynamoClient.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), default))
            .ReturnsAsync(new QueryResponse 
            { 
                Items = new List<Dictionary<string, AttributeValue>> 
                { 
                    new() 
                    { 
                        ["SK"] = new AttributeValue("METADATA"),
                        ["data"] = new AttributeValue { M = new Dictionary<string, AttributeValue> { ["name"] = new AttributeValue("BCI Training Study") } },
                        ["questionnaireConfig"] = new AttributeValue { M = new Dictionary<string, AttributeValue>() }
                    } 
                } 
            });

        // Act
        var dailyResult = await _service.CreateSessionAsync("BCI_TRAINING_21D", dailyRequest, "testuser");
        var weeklyResult = await _service.CreateSessionAsync("BCI_TRAINING_21D", weeklyRequest, "testuser");
        var traitResult = await _service.CreateSessionAsync("BCI_TRAINING_21D", traitRequest, "testuser");

        // Assert
        Assert.NotNull(dailyResult);
        Assert.NotNull(weeklyResult);
        Assert.NotNull(traitResult);
        _mockDynamoClient.Verify(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default), Times.Exactly(3));
    }
}