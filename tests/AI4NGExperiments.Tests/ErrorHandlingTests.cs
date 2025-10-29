using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGExperimentsLambda.Controllers;
using AI4NGExperimentsLambda.Interfaces;
using AI4NGExperimentsLambda.Services;
using AI4NGExperimentsLambda.Models;
using System.ComponentModel.DataAnnotations;
using AI4NGExperimentManagement.Shared;
using AI4NGExperimentManagementTests.Shared;

namespace AI4NGExperiments.Tests;

public class ErrorHandlingTests : ControllerTestBase<ExperimentsController>
{
    private readonly Mock<IExperimentService> _mockExperimentService;
    private readonly Mock<IAmazonDynamoDB> _mockDynamoClient;
    private readonly ExperimentsController _controller;
    private readonly ExperimentService _service;

    private (Mock<IExperimentService> mockService, ExperimentsController controller, Mock<IAuthenticationService> authMock) CreateController(bool isLocal = true)
    {
        var mockService = new Mock<IExperimentService>();
        var authMock = CreateAuthMock();
        var controller = CreateControllerWithContext(new ExperimentsController(mockService.Object, authMock.Object), isLocal);
        return (mockService, controller, authMock);
    }

    public ErrorHandlingTests()
    {
        _mockExperimentService = new Mock<IExperimentService>();
        _mockDynamoClient = new Mock<IAmazonDynamoDB>();

        Environment.SetEnvironmentVariable("EXPERIMENTS_TABLE", "experiments-test");
        Environment.SetEnvironmentVariable("RESPONSES_TABLE", "responses-test");
        Environment.SetEnvironmentVariable("QUESTIONNAIRES_TABLE", "questionnaires-test");

        var mockAuth = new Mock<IAuthenticationService>();
        _controller = new ExperimentsController(_mockExperimentService.Object, mockAuth.Object);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        _service = new ExperimentService(_mockDynamoClient.Object);
    }


    [Fact]
    public async Task Create_ShouldReturn401_WhenNoAuthHeader()
    {
        // Arrange
        var (mockService, controller, authMock) = CreateController(isLocal: false);
        authMock.Setup(x => x.GetUsernameFromRequest()).Throws(new UnauthorizedAccessException("Authorization header is required"));
        var experiment = new Experiment();

        // Act
        var result = await controller.Create(experiment);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Create_ShouldReturn401_WhenInvalidToken()
    {
        // Arrange
        var (mockService, controller, authMock) = CreateController(isLocal: false);
        authMock.Setup(x => x.GetUsernameFromRequest()).Throws(new UnauthorizedAccessException("Invalid token format"));
        var experiment = new Experiment();

        // Act
        var result = await controller.Create(experiment);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Create_ShouldReturn401_WhenMalformedAuthHeader()
    {
        // Arrange
        var (mockService, controller, authMock) = CreateController(isLocal: false);
        authMock.Setup(x => x.GetUsernameFromRequest()).Throws(new UnauthorizedAccessException("Bearer token required"));
        var experiment = new Experiment();

        // Act
        var result = await controller.Create(experiment);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Service_ShouldThrowException_WhenDynamoDBConnectionFails()
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.ScanAsync(It.IsAny<ScanRequest>(), default))
            .ThrowsAsync(new AmazonDynamoDBException("Connection failed"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<AmazonDynamoDBException>(
            () => _service.GetExperimentsAsync());

        Assert.Contains("Connection failed", exception.Message);
    }

    [Fact]
    public async Task Service_ShouldThrowException_WhenInvalidRequestData()
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ThrowsAsync(new ValidationException("Invalid request data"));

        var experiment = new Experiment
        {
            Data = new ExperimentData { Name = "Test" },
            QuestionnaireConfig = new QuestionnaireConfig()
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _service.CreateExperimentAsync(experiment, "testuser"));

        Assert.Contains("Invalid request data", exception.Message);
    }

    [Fact]
    public async Task Service_ShouldHandleTimeout_WhenOperationTakesTooLong()
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), default))
            .ThrowsAsync(new TaskCanceledException("Operation timed out"));

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => _service.GetExperimentAsync("test-id"));
    }

    [Fact]
    public async Task Service_ShouldHandleResourceNotFound_WhenTableDoesNotExist()
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.ScanAsync(It.IsAny<ScanRequest>(), default))
            .ThrowsAsync(new ResourceNotFoundException("Table not found"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => _service.GetExperimentsAsync());

        Assert.Contains("Table not found", exception.Message);
    }

    [Fact]
    public async Task Service_ShouldHandleProvisionedThroughputExceeded()
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ThrowsAsync(new ProvisionedThroughputExceededException("Rate limit exceeded"));

        var experiment = new Experiment
        {
            Data = new ExperimentData { Name = "Test" },
            QuestionnaireConfig = new QuestionnaireConfig()
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ProvisionedThroughputExceededException>(
            () => _service.CreateExperimentAsync(experiment, "testuser"));

        Assert.Contains("Rate limit exceeded", exception.Message);
    }

    [Fact]
    public async Task Service_ShouldHandleConditionalCheckFailed()
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .ThrowsAsync(new ConditionalCheckFailedException("Condition not met"));

        var data = new ExperimentData { Name = "Updated" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ConditionalCheckFailedException>(
            () => _service.UpdateExperimentAsync("test-id", data, "testuser"));

        Assert.Contains("Condition not met", exception.Message);
    }

    [Fact]
    public async Task Service_ShouldHandleItemSizeTooLarge()
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ThrowsAsync(new LimitExceededException("Item too large"));

        var experiment = new Experiment
        {
            Data = new ExperimentData { Name = "Test" },
            QuestionnaireConfig = new QuestionnaireConfig()
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<LimitExceededException>(
            () => _service.CreateExperimentAsync(experiment, "testuser"));

        Assert.Contains("Item too large", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Service_ShouldHandleInvalidExperimentIds(string? invalidId)
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), default))
            .ReturnsAsync(new QueryResponse { Items = new List<Dictionary<string, AttributeValue>>() });

        // Act
        var result = await _service.GetExperimentAsync(invalidId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Service_ShouldHandleNullSyncTime()
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse
            {
                IsItemSet = true,
                Item = new Dictionary<string, AttributeValue> { ["PK"] = new AttributeValue("EXPERIMENT#test-id") }
            });

        _mockDynamoClient.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), default))
            .ReturnsAsync(new QueryResponse { Items = new List<Dictionary<string, AttributeValue>>() });

        // Act - This should not crash with null sync time
        var result = await _service.SyncExperimentAsync("test-id", null, "testuser");

        // Assert - Should not throw exception
        Assert.NotNull(result);
        _mockDynamoClient.Verify(x => x.QueryAsync(It.IsAny<QueryRequest>(), default), Times.Once);
    }
}