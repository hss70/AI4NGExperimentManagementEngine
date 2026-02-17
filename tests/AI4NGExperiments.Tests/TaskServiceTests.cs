using Xunit;
using Moq;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGExperimentsLambda.Services;
using AI4NGExperimentsLambda.Models;

namespace AI4NGExperiments.Tests;

public class TaskServiceTests
{
    private readonly Mock<IAmazonDynamoDB> _mockDynamoClient;
    private readonly TaskService _service;

    public TaskServiceTests()
    {
        _mockDynamoClient = new Mock<IAmazonDynamoDB>();
        Environment.SetEnvironmentVariable("EXPERIMENTS_TABLE", "experiments-test");

        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ReturnsAsync(new PutItemResponse());

        // Default GetItemAsync to return an item (questionnaires lookups) so questionnaire existence checks pass
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse
            {
                IsItemSet = true,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue("QUESTIONNAIRE#exists")
                }
            });

        _service = new TaskService(_mockDynamoClient.Object);
    }

    [Fact]
    public async Task CreateTaskAsync_ShouldReturnTaskId_WhenValid()
    {
        // Arrange
        var request = new CreateTaskRequest
        {
            TaskKey = "TEST_TASK_KEY",
            Data = new TaskData
            {
                Name = "Test Task",
                Type = "Training",
                Description = "Test description",
                EstimatedDuration = 300
            }
        };

        // Act
        var result = await _service.CreateTaskAsync(request, "testuser");

        // Assert
        Assert.NotNull(result);
        _mockDynamoClient.Verify(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task GetTasksAsync_ShouldReturnTasks()
    {
        // Arrange
        var queryResponse = new QueryResponse
        {
            Items = new List<Dictionary<string, AttributeValue>>
            {
                new()
                {
                    ["PK"] = new AttributeValue("TASK#task-1"),
                    ["data"] = new AttributeValue { M = new Dictionary<string, AttributeValue> { ["name"] = new AttributeValue("Test Task") } },
                    ["createdAt"] = new AttributeValue(DateTime.UtcNow.ToString("O")),
                    ["updatedAt"] = new AttributeValue(DateTime.UtcNow.ToString("O"))
                }
            }
        };

        _mockDynamoClient.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), default))
            .ReturnsAsync(queryResponse);

        // Act
        var result = await _service.GetTasksAsync();

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public async Task GetTaskAsync_ShouldReturnTask_WhenExists()
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse
            {
                IsItemSet = true,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["data"] = new AttributeValue { M = new Dictionary<string, AttributeValue> { ["name"] = new AttributeValue("Test Task") } },
                    ["createdAt"] = new AttributeValue(DateTime.UtcNow.ToString("O")),
                    ["updatedAt"] = new AttributeValue(DateTime.UtcNow.ToString("O"))
                }
            });

        // Act
        var result = await _service.GetTaskAsync("task-1");

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task UpdateTaskAsync_ShouldCallUpdateItem()
    {
        // Arrange
        var data = new TaskData { Name = "Updated Task", Type = "Training" };
        _mockDynamoClient.Setup(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .ReturnsAsync(new UpdateItemResponse());

        // Act
        await _service.UpdateTaskAsync("TASK1", data, "testuser");

        // Assert
        _mockDynamoClient.Verify(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task DeleteTaskAsync_ShouldCallDeleteItem()
    {
        // Arrange: service performs a soft-delete using UpdateItemAsync
        _mockDynamoClient.Setup(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .ReturnsAsync(new UpdateItemResponse());

        // Act
        await _service.DeleteTaskAsync("task-1", "testuser");

        // Assert
        _mockDynamoClient.Verify(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task CreateTaskAsync_ShouldHandleQuestionnaireTask()
    {
        // Arrange - Single questionnaire task
        var request = new CreateTaskRequest
        {
            TaskKey = "pre_training_state_questionnaire",
            Data = new TaskData
            {
                Name = "Pre-Training State Questionnaire",
                Type = "Questionnaire",
                Description = "Assesses emotional and cognitive readiness before BCI training."
                ,
                QuestionnaireIds = new List<string> { "preq1" }
            }
        };

        // Act
        var result = await _service.CreateTaskAsync(request, "testuser");

        // Assert
        Assert.NotNull(result);
        _mockDynamoClient.Verify(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task CreateTaskAsync_ShouldHandleBatchQuestionnaireTask()
    {
        // Arrange - Batch questionnaire task
        var request = new CreateTaskRequest
        {
            TaskKey = "trait_questionnaire_bank",
            Data = new TaskData
            {
                Name = "Trait Questionnaire Bank",
                Type = "QuestionnaireSet",
                Description = "Presents one or more trait questionnaires (once per study)."
                ,
                QuestionnaireIds = new List<string> { "trait_q1" }
            }
        };

        // Act
        var result = await _service.CreateTaskAsync(request, "testuser");

        // Assert
        Assert.NotNull(result);
        _mockDynamoClient.Verify(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task CreateTaskAsync_ShouldHandleEEGTrainingTask()
    {
        // Arrange - EEG training task
        var request = new CreateTaskRequest
        {
            TaskKey = "eeg_training_session",
            Data = new TaskData
            {
                Name = "EEG Training Session",
                Type = "Training",
                Description = "EEG-based neurofeedback training session."
            }
        };

        // Act
        var result = await _service.CreateTaskAsync(request, "testuser");

        // Assert
        Assert.NotNull(result);
        _mockDynamoClient.Verify(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task CreateTaskAsync_ShouldThrow_WhenTaskKeyMissing()
    {
        // Arrange - missing TaskKey
        var request = new CreateTaskRequest
        {
            TaskKey = string.Empty,
            Data = new TaskData
            {
                Name = "No Key Task",
                Type = "eeg_training",
                Description = "Missing key"
            }
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateTaskAsync(request, "testuser"));
        Assert.Contains("TaskKey is required", ex.Message);
    }

    [Fact]
    public async Task CreateTaskAsync_ShouldThrow_WhenTaskKeyFormatInvalid()
    {
        // Arrange - invalid format (contains hyphen and lowercase but hyphen is the issue)
        var request = new CreateTaskRequest
        {
            TaskKey = "bad-key!",
            Data = new TaskData
            {
                Name = "Bad Key Task",
                Type = "eeg_training",
                Description = "Invalid format"
            }
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateTaskAsync(request, "testuser"));
        Assert.Contains("Invalid TaskKey format", ex.Message);
    }
}