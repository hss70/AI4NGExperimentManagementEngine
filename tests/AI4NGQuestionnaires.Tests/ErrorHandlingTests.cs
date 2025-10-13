using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGQuestionnairesLambda.Controllers;
using AI4NGQuestionnairesLambda.Interfaces;
using AI4NGQuestionnairesLambda.Services;
using AI4NGQuestionnairesLambda.Models;
using AI4NGExperimentManagement.Shared;
using System.ComponentModel.DataAnnotations;

namespace AI4NGQuestionnaires.Tests;

[Collection("QuestionnairesCollection")]
public class ErrorHandlingTests
{
    private readonly Mock<IQuestionnaireService> _mockQuestionnaireService;
    private readonly Mock<IAmazonDynamoDB> _mockDynamoClient;
    private readonly QuestionnairesController _controller;
    private readonly QuestionnaireService _service;

    public ErrorHandlingTests()
    {
        _mockQuestionnaireService = new Mock<IQuestionnaireService>();
        _mockDynamoClient = new Mock<IAmazonDynamoDB>();

        Environment.SetEnvironmentVariable("QUESTIONNAIRES_TABLE", "test-table");

        var mockAuth = new Mock<IAuthenticationService>();
        _controller = new QuestionnairesController(_mockQuestionnaireService.Object, mockAuth.Object);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        _service = new QuestionnaireService(_mockDynamoClient.Object);
    }


    [Fact]
    public async Task Create_ShouldReturn401_WhenNoAuthHeader()
    {
        // Arrange
        Environment.SetEnvironmentVariable("AWS_ENDPOINT_URL", null);
        _controller.ControllerContext.HttpContext.Request.Headers.Clear();
        var request = new CreateQuestionnaireRequest { Id = "test", Data = new QuestionnaireData { Name = "Test" } };

        // Act
        var result = await _controller.Create(request);

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
            () => _service.GetAllAsync());

        Assert.Contains("Connection failed", exception.Message);
    }

    [Fact]
    public async Task Service_ShouldThrowException_WhenInvalidRequestData()
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ThrowsAsync(new ValidationException("Invalid request data"));

        var request = new CreateQuestionnaireRequest
        {
            Id = "test",
            Data = new QuestionnaireData { Name = "Test" }
        };

        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse { Item = null });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _service.CreateAsync(request, "testuser"));

        Assert.Contains("Invalid request data", exception.Message);
    }

    [Fact]
    public async Task Service_ShouldHandleTimeout_WhenOperationTakesTooLong()
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ThrowsAsync(new TaskCanceledException("Operation timed out"));

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => _service.GetByIdAsync("test-id"));
    }

    [Fact]
    public async Task Service_ShouldHandleResourceNotFound_WhenTableDoesNotExist()
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.ScanAsync(It.IsAny<ScanRequest>(), default))
            .ThrowsAsync(new ResourceNotFoundException("Table not found"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => _service.GetAllAsync());

        Assert.Contains("Table not found", exception.Message);
    }

    [Fact]
    public async Task Service_ShouldHandleProvisionedThroughputExceeded()
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ThrowsAsync(new ProvisionedThroughputExceededException("Rate limit exceeded"));

        var request = new CreateQuestionnaireRequest
        {
            Id = "test",
            Data = new QuestionnaireData { Name = "Test" }
        };

        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse { Item = null });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ProvisionedThroughputExceededException>(
            () => _service.CreateAsync(request, "testuser"));

        Assert.Contains("Rate limit exceeded", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Service_ShouldHandleInvalidQuestionnaireIds(string? invalidId)
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse { Item = null });

        // Act
        var result = await _service.GetByIdAsync(invalidId!);

        // Assert
        Assert.Null(result);
    }
}