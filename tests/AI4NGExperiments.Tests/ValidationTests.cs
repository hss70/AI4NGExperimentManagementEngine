using Moq;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGExperimentsLambda.Services;
using AI4NGExperimentsLambda.Models;
using AI4NGExperimentsLambda.Models.Requests;

namespace AI4NGExperiments.Tests;

public class ValidationTests
{
    private readonly Mock<IAmazonDynamoDB> _mockDynamoClient;
    private readonly ExperimentsService _service;

    public ValidationTests()
    {
        _mockDynamoClient = new Mock<IAmazonDynamoDB>();
        Environment.SetEnvironmentVariable("EXPERIMENTS_TABLE", "experiments-test");
        Environment.SetEnvironmentVariable("RESPONSES_TABLE", "responses-test");
        Environment.SetEnvironmentVariable("QUESTIONNAIRES_TABLE", "questionnaires-test");

        _service = new ExperimentsService(_mockDynamoClient.Object);
    }

    [Fact]
    public async Task CreateExperimentAsync_ShouldThrowException_WhenNameIsEmpty()
    {
        // Arrange
        var experiment = new CreateExperimentRequest()
        {
            Data = new ExperimentData { Name = "", Description = "Valid description" }, // Empty name
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateExperimentAsync(experiment, "testuser"));
    }

    [Fact]
    public async Task CreateExperimentAsync_ShouldThrowException_WhenNameIsWhitespace()
    {
        // Arrange
        var experiment = new CreateExperimentRequest
        {
            Data = new ExperimentData { Name = "   ", Description = "Valid description" }, // Whitespace only
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateExperimentAsync(experiment, "testuser"));
    }

    [Fact]
    public async Task CreateExperimentAsync_ShouldSucceed_WhenValidData()
    {
        // Arrange
        var experiment = new CreateExperimentRequest
        {
            Data = new ExperimentData
            {
                Name = "Valid Experiment Name",
                Description = "Valid description"
            },
        };

        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ReturnsAsync(new PutItemResponse());

        // Act
        var result = await _service.CreateExperimentAsync(experiment, "testuser");

        // Assert
        Assert.NotNull(result);
        _mockDynamoClient.Verify(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default), Times.Once);
    }



    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task CreateExperimentAsync_ShouldHandleInvalidUsernames(string? username)
    {
        // Arrange
        var experiment = new CreateExperimentRequest
        {
            Data = new ExperimentData { Name = "Test Experiment", Description = "Test Description" },
        };

        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ReturnsAsync(new PutItemResponse());

        // Act & Assert - service requires a non-empty performedBy and will throw ArgumentException
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateExperimentAsync(experiment, username!));
        Assert.Contains("Authentication required", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task GetExperimentAsync_ShouldHandleInvalidExperimentIds(string? experimentId)
    {
        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.GetExperimentAsync(experimentId!));
        Assert.Contains("Experiment ID is required", ex.Message);
    }
}
