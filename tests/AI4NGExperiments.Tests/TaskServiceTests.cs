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

        _service = new TaskService(_mockDynamoClient.Object);
    }

    [Fact]
    public async Task CreateTaskAsync_ShouldReturnTaskId_WhenValid()
    {
        // Arrange
        var request = new CreateTaskRequest
        {
            TaskKey = "TEST_TASK_KEY",
            Name = "Test Task",
            Type = "TRAIN_EEG",
            Description = "Test description",
            EstimatedDuration = 300
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
        var data = new TaskData { Name = "Updated Task" };
        _mockDynamoClient.Setup(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .ReturnsAsync(new UpdateItemResponse());

        // Act
        await _service.UpdateTaskAsync("task-1", data, "testuser");

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
            Name = "Pre-Training State Questionnaire",
            Type = "questionnaire",
            Description = "Assesses emotional and cognitive readiness before BCI training."
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
            Name = "Trait Questionnaire Bank",
            Type = "questionnaire_batch",
            Description = "Presents one or more trait questionnaires (once per study)."
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
            Name = "EEG Training Session",
            Type = "eeg_training",
            Description = "EEG-based neurofeedback training session."
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
            Name = "No Key Task",
            Type = "eeg_training",
            Description = "Missing key"
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
            Name = "Bad Key Task",
            Type = "eeg_training",
            Description = "Invalid format"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateTaskAsync(request, "testuser"));
        Assert.Contains("Invalid TaskKey format", ex.Message);
    }
}