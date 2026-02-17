using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGQuestionnairesLambda.Controllers;
using AI4NGQuestionnairesLambda.Interfaces;
using AI4NGQuestionnairesLambda.Services;
using AI4NG.ExperimentManagement.Contracts.Questionnaires;
using AI4NGExperimentManagement.Shared;
using System.ComponentModel.DataAnnotations;

namespace AI4NGQuestionnaires.Tests;

[Collection("QuestionnairesCollection")]
public class ErrorHandlingTests
{
    private readonly Mock<IQuestionnaireService> _mockQuestionnaireService;
    private readonly Mock<IAmazonDynamoDB> _mockDynamoClient;
    private readonly Mock<IAuthenticationService> _mockAuth;
    private readonly QuestionnairesController _controller;
    private readonly QuestionnaireService _service;

    public ErrorHandlingTests()
    {
        _mockQuestionnaireService = new Mock<IQuestionnaireService>();
        _mockDynamoClient = new Mock<IAmazonDynamoDB>();

        Environment.SetEnvironmentVariable("QUESTIONNAIRES_TABLE", "test-table");

        _mockAuth = new Mock<IAuthenticationService>();
        _mockAuth.Setup(x => x.GetUsernameFromRequest()).Returns("testuser");
        _mockAuth.Setup(x => x.IsResearcher()).Returns(true);

        _controller = new QuestionnairesController(_mockQuestionnaireService.Object, _mockAuth.Object);
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
        var request = new CreateQuestionnaireRequest { Id = "test", Data = new QuestionnaireDataDto { Name = "Test" } };

        // Act & Assert - controller directly uses the auth service; if no username is returned
        // GetAuthenticatedUsername will throw UnauthorizedAccessException which the test should expect
        _mockAuth.Setup(x => x.GetUsernameFromRequest()).Returns(string.Empty);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _controller.Create(request, System.Threading.CancellationToken.None));
    }

    [Fact]
    public async Task Service_ShouldThrowException_WhenDynamoDBConnectionFails()
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<System.Threading.CancellationToken>()))
            .ThrowsAsync(new AmazonDynamoDBException("Connection failed"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<AmazonDynamoDBException>(
            () => _service.GetAllAsync(System.Threading.CancellationToken.None));

        Assert.Contains("Connection failed", exception.Message);
    }

    [Fact]
    public async Task Service_ShouldThrowException_WhenInvalidRequestData()
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<System.Threading.CancellationToken>()))
            .ThrowsAsync(new ValidationException("Invalid request data"));

        var request = new CreateQuestionnaireRequest
        {
            Id = "test",
            Data = new QuestionnaireDataDto
            {
                Name = "Test",
                Questions = new List<QuestionDto>
                {
                    new QuestionDto { Id = "1", Text = "Q1", Type = "text" }
                }
            }
        };

        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new GetItemResponse { Item = null });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _service.CreateAsync(request.Id, request.Data, "testuser", System.Threading.CancellationToken.None));

        Assert.Contains("Invalid request data", exception.Message);
    }

    [Fact]
    public async Task Service_ShouldHandleTimeout_WhenOperationTakesTooLong()
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<System.Threading.CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("Operation timed out"));

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => _service.GetByIdAsync("test-id", System.Threading.CancellationToken.None));
    }

    [Fact]
    public async Task Service_ShouldHandleResourceNotFound_WhenTableDoesNotExist()
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<System.Threading.CancellationToken>()))
            .ThrowsAsync(new ResourceNotFoundException("Table not found"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => _service.GetAllAsync(System.Threading.CancellationToken.None));

        Assert.Contains("Table not found", exception.Message);
    }

    [Fact]
    public async Task Service_ShouldHandleProvisionedThroughputExceeded()
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<System.Threading.CancellationToken>()))
            .ThrowsAsync(new ProvisionedThroughputExceededException("Rate limit exceeded"));

        var request = new CreateQuestionnaireRequest
        {
            Id = "test",
            Data = new QuestionnaireDataDto
            {
                Name = "Test",
                Questions = new List<QuestionDto>
                {
                    new QuestionDto { Id = "1", Text = "Q1", Type = "text" }
                }
            }
        };

        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new GetItemResponse { Item = null });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ProvisionedThroughputExceededException>(
            () => _service.CreateAsync(request.Id, request.Data, "testuser", System.Threading.CancellationToken.None));

        Assert.Contains("Rate limit exceeded", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Service_ShouldHandleInvalidQuestionnaireIds(string? invalidId)
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new GetItemResponse { Item = null });

        // Act
        var result = await _service.GetByIdAsync(invalidId!, System.Threading.CancellationToken.None);

        // Assert
        Assert.Null(result);
    }
}