using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGResponsesLambda.Controllers;
using AI4NGResponsesLambda.Interfaces;
using AI4NGResponsesLambda.Services;
using AI4NGResponsesLambda.Models;
using AI4NGExperimentManagement.Shared;
using System.ComponentModel.DataAnnotations;
using AI4NGExperimentManagementTests.Shared;

namespace AI4NGResponses.Tests;

public class ErrorHandlingTests : ControllerTestBase<ResponsesController>
{
    private readonly Mock<IResponseService> _mockResponseService;
    private readonly Mock<IAmazonDynamoDB> _mockDynamoClient;
    private readonly ResponsesController _controller;
    private readonly ResponseService _service;

    private (Mock<IResponseService> mockService, ResponsesController controller, Mock<IAuthenticationService> authMock) CreateController(bool isLocal = true)
    {
        var mockService = new Mock<IResponseService>();
        var authMock = CreateAuthMock();
        var controller = CreateControllerWithContext(new ResponsesController(mockService.Object, authMock.Object), isLocal);
        return (mockService, controller, authMock);
    }

    public ErrorHandlingTests()
    {
        _mockResponseService = new Mock<IResponseService>();
        _mockDynamoClient = new Mock<IAmazonDynamoDB>();

        Environment.SetEnvironmentVariable("RESPONSES_TABLE", "responses-test");

        var mockAuth = new Mock<IAuthenticationService>();
        _controller = new ResponsesController(_mockResponseService.Object, mockAuth.Object);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        _service = new ResponseService(_mockDynamoClient.Object);
    }


    [Fact]
    public async Task Create_ShouldReturn401_WhenNoAuthHeader()
    {
        // Arrange
        var (mockService, controller, authMock) = CreateController(isLocal: false);
        authMock.Setup(x => x.GetUsernameFromRequest()).Throws(new UnauthorizedAccessException("Authorization header is required"));
        var response = new Response();

        // Act
        var result = await controller.Create(response);

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
            () => _service.GetResponsesAsync());

        Assert.Contains("Connection failed", exception.Message);
    }

    [Fact]
    public async Task Service_ShouldThrowException_WhenInvalidRequestData()
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ThrowsAsync(new ValidationException("Invalid request data"));

        var response = new Response
        {
            Data = new ResponseData { ExperimentId = "test" }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _service.CreateResponseAsync(response, "testuser"));

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
            () => _service.GetResponseAsync("test-id"));
    }

    [Fact]
    public async Task Service_ShouldHandleResourceNotFound_WhenTableDoesNotExist()
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.ScanAsync(It.IsAny<ScanRequest>(), default))
            .ThrowsAsync(new ResourceNotFoundException("Table not found"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => _service.GetResponsesAsync());

        Assert.Contains("Table not found", exception.Message);
    }

    [Fact]
    public async Task Service_ShouldHandleProvisionedThroughputExceeded()
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ThrowsAsync(new ProvisionedThroughputExceededException("Rate limit exceeded"));

        var response = new Response
        {
            Data = new ResponseData { ExperimentId = "test" }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ProvisionedThroughputExceededException>(
            () => _service.CreateResponseAsync(response, "testuser"));

        Assert.Contains("Rate limit exceeded", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Service_ShouldHandleInvalidResponseIds(string? invalidId)
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse { Item = null });

        // Act
        var result = await _service.GetResponseAsync(invalidId);

        // Assert
        Assert.Null(result);
    }
}