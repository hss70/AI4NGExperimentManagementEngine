using Moq;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGExperimentsLambda.Services;
using AI4NGExperimentsLambda.Models;
using AI4NGExperimentsLambda.Models.Requests;

namespace AI4NGExperiments.Tests;

public class TaskServiceTests
{
    private readonly Mock<IAmazonDynamoDB> _mockDynamoClient;
    private readonly TaskService _service;

    public TaskServiceTests()
    {
        _mockDynamoClient = new Mock<IAmazonDynamoDB>();

        Environment.SetEnvironmentVariable("EXPERIMENTS_TABLE", "experiments-test");
        Environment.SetEnvironmentVariable("QUESTIONNAIRES_TABLE", "questionnaires-test");

        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ReturnsAsync(new PutItemResponse());

        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse
            {
                IsItemSet = true,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue("QUESTIONNAIRE#exists")
                }
            });

        _mockDynamoClient.Setup(x => x.BatchGetItemAsync(It.IsAny<BatchGetItemRequest>(), default))
            .ReturnsAsync((BatchGetItemRequest req, CancellationToken _) =>
            {
                var tableName = req.RequestItems.Keys.Single();
                var requestedKeys = req.RequestItems[tableName].Keys;

                var items = requestedKeys.Select(k =>
                {
                    var pk = k["PK"].S;
                    return new Dictionary<string, AttributeValue>
                    {
                        ["PK"] = new AttributeValue(pk),
                        ["SK"] = new AttributeValue("CONFIG"),
                        ["syncMetadata"] = new AttributeValue
                        {
                            M = new Dictionary<string, AttributeValue>
                            {
                                ["isDeleted"] = new AttributeValue { BOOL = false }
                            }
                        }
                    };
                }).ToList();

                return new BatchGetItemResponse
                {
                    Responses = new Dictionary<string, List<Dictionary<string, AttributeValue>>>
                    {
                        [tableName] = items
                    },
                    UnprocessedKeys = new Dictionary<string, KeysAndAttributes>()
                };
            });

        _mockDynamoClient.Setup(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .ReturnsAsync(new UpdateItemResponse());

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
                EstimatedDuration = 300,
                Configuration = new Dictionary<string, object>
                {
                    ["sceneName"] = "NeuroSensi1"
                }
            }
        };

        // Act
        var result = await _service.CreateTaskAsync(request, "testuser");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TEST_TASK_KEY", result.Id);
        _mockDynamoClient.Verify(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task GetTasksAsync_ShouldReturnTasks()
    {
        // Arrange
        var now = DateTime.UtcNow.ToString("O");

        var queryResponse = new QueryResponse
        {
            Items = new List<Dictionary<string, AttributeValue>>
            {
                new()
                {
                    ["PK"] = new AttributeValue("TASK#TASK_1"),
                    ["data"] = new AttributeValue
                    {
                        M = new Dictionary<string, AttributeValue>
                        {
                            ["Name"] = new AttributeValue("Test Task"),
                            ["Type"] = new AttributeValue("Training"),
                            ["Description"] = new AttributeValue("Test description"),
                            ["EstimatedDuration"] = new AttributeValue { N = "300" },
                            ["QuestionnaireIds"] = new AttributeValue { L = new List<AttributeValue>() },
                            ["Configuration"] = new AttributeValue
                            {
                                M = new Dictionary<string, AttributeValue>
                                {
                                    ["sceneName"] = new AttributeValue("NeuroSensi1")
                                }
                            }
                        }
                    },
                    ["createdAt"] = new AttributeValue(now),
                    ["updatedAt"] = new AttributeValue(now)
                }
            }
        };

        _mockDynamoClient.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), default))
            .ReturnsAsync(queryResponse);

        // Act
        var result = (await _service.GetTasksAsync()).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal("TASK_1", result[0].TaskKey);
        Assert.Equal("Training", result[0].Data.Type);
    }

    [Fact]
    public async Task GetTaskAsync_ShouldReturnTask_WhenExists()
    {
        // Arrange
        var now = DateTime.UtcNow.ToString("O");

        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse
            {
                IsItemSet = true,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue("TASK#TASK_1"),
                    ["data"] = new AttributeValue
                    {
                        M = new Dictionary<string, AttributeValue>
                        {
                            ["Name"] = new AttributeValue("Test Task"),
                            ["Type"] = new AttributeValue("Training"),
                            ["Description"] = new AttributeValue("Test description"),
                            ["EstimatedDuration"] = new AttributeValue { N = "300" },
                            ["QuestionnaireIds"] = new AttributeValue { L = new List<AttributeValue>() },
                            ["Configuration"] = new AttributeValue
                            {
                                M = new Dictionary<string, AttributeValue>
                                {
                                    ["sceneName"] = new AttributeValue("NeuroSensi1")
                                }
                            }
                        }
                    },
                    ["createdAt"] = new AttributeValue(now),
                    ["updatedAt"] = new AttributeValue(now)
                }
            });

        // Act
        var result = await _service.GetTaskAsync("task_1");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TASK_1", result!.TaskKey);
        Assert.Equal("Training", result.Data.Type);
    }

    [Fact]
    public async Task UpdateTaskAsync_ShouldCallUpdateItem()
    {
        // Arrange
        var data = new TaskData
        {
            Name = "Updated Task",
            Type = "Training",
            Description = "Updated description",
            EstimatedDuration = 300,
            Configuration = new Dictionary<string, object>
            {
                ["sceneName"] = "NeuroSensi1"
            }
        };

        // Act
        await _service.UpdateTaskAsync("TASK1", data, "testuser");

        // Assert
        _mockDynamoClient.Verify(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task DeleteTaskAsync_ShouldCallUpdateItem()
    {
        // Act
        await _service.DeleteTaskAsync("task-1", "testuser");

        // Assert
        _mockDynamoClient.Verify(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task CreateTaskAsync_ShouldHandleQuestionnaireTask()
    {
        // Arrange
        var request = new CreateTaskRequest
        {
            TaskKey = "PRE_TRAINING_STATE_QUESTIONNAIRE",
            Data = new TaskData
            {
                Name = "Pre-Training State Questionnaire",
                Type = "Questionnaire",
                Description = "Assesses emotional and cognitive readiness before BCI training.",
                QuestionnaireIds = new List<string> { "preq1" },
                Configuration = new Dictionary<string, object>()
            }
        };

        // Act
        var result = await _service.CreateTaskAsync(request, "testuser");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("PRE_TRAINING_STATE_QUESTIONNAIRE", result.Id);
        _mockDynamoClient.Verify(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task CreateTaskAsync_ShouldHandleBatchQuestionnaireTask()
    {
        // Arrange
        var request = new CreateTaskRequest
        {
            TaskKey = "TRAIT_QUESTIONNAIRE_BANK",
            Data = new TaskData
            {
                Name = "Trait Questionnaire Bank",
                Type = "QuestionnaireSet",
                Description = "Presents one or more trait questionnaires (once per study).",
                QuestionnaireIds = new List<string> { "trait_q1" },
                Configuration = new Dictionary<string, object>()
            }
        };

        // Act
        var result = await _service.CreateTaskAsync(request, "testuser");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TRAIT_QUESTIONNAIRE_BANK", result.Id);
        _mockDynamoClient.Verify(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task CreateTaskAsync_ShouldHandleEEGTrainingTask()
    {
        // Arrange
        var request = new CreateTaskRequest
        {
            TaskKey = "EEG_TRAINING_SESSION",
            Data = new TaskData
            {
                Name = "EEG Training Session",
                Type = "Training",
                Description = "EEG-based neurofeedback training session.",
                Configuration = new Dictionary<string, object>
                {
                    ["sceneName"] = "NeuroSensi1",
                    ["hasFeedback"] = true
                }
            }
        };

        // Act
        var result = await _service.CreateTaskAsync(request, "testuser");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("EEG_TRAINING_SESSION", result.Id);
        _mockDynamoClient.Verify(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task CreateTaskAsync_ShouldThrow_WhenTaskKeyMissing()
    {
        // Arrange
        var request = new CreateTaskRequest
        {
            TaskKey = string.Empty,
            Data = new TaskData
            {
                Name = "No Key Task",
                Type = "Training",
                Description = "Missing key",
                Configuration = new Dictionary<string, object>
                {
                    ["sceneName"] = "NeuroSensi1"
                }
            }
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateTaskAsync(request, "testuser"));
        Assert.Contains("TaskKey is required", ex.Message);
    }

    [Fact]
    public async Task CreateTaskAsync_ShouldThrow_WhenTaskKeyFormatInvalid()
    {
        // Arrange
        var request = new CreateTaskRequest
        {
            TaskKey = "bad-key!",
            Data = new TaskData
            {
                Name = "Bad Key Task",
                Type = "Training",
                Description = "Invalid format",
                Configuration = new Dictionary<string, object>
                {
                    ["sceneName"] = "NeuroSensi1"
                }
            }
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateTaskAsync(request, "testuser"));
        Assert.Contains("Invalid TaskKey format", ex.Message);
    }
}