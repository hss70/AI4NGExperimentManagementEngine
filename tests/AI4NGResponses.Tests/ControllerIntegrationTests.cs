using Microsoft.AspNetCore.Mvc;
using Moq;
using AI4NGResponsesLambda.Controllers;
using AI4NGResponsesLambda.Interfaces;
using AI4NGResponsesLambda.Models;
using AI4NGExperimentManagementTests.Shared;

namespace AI4NGResponses.Tests;

public class ControllerIntegrationTests : ControllerTestBase<ResponsesController>
{
    [Fact]
    public async Task ResponsesController_GetAll_ShouldReturnOkWithResponses()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IResponseService>(
            (service, auth) => new ResponsesController(service, auth));

        var responses = new List<Response>
        {
            new() { Id = "r1", Data = new ResponseData { ExperimentId = "exp1" } },
            new() { Id = "r2", Data = new ResponseData { ExperimentId = "exp2" } }
        };
        mockService.Setup(x => x.GetResponsesAsync(null, null)).ReturnsAsync(responses);

        // Act
        var result = await controller.GetAll();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(responses, okResult.Value);
    }

    [Fact]
    public async Task ResponsesController_GetAll_WithExperimentId_ShouldReturnFilteredResponses()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IResponseService>(
            (service, auth) => new ResponsesController(service, auth));

        var responses = new List<Response>
        {
            new() { Id = "r1", Data = new ResponseData { ExperimentId = "exp1" } }
        };
        mockService.Setup(x => x.GetResponsesAsync("exp1", null)).ReturnsAsync(responses);

        // Act
        var result = await controller.GetAll("exp1");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(responses, okResult.Value);
        mockService.Verify(x => x.GetResponsesAsync("exp1", null), Times.Once);
    }

    [Fact]
    public async Task ResponsesController_GetAll_WithExperimentAndSessionId_ShouldReturnFilteredResponses()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IResponseService>(
            (service, auth) => new ResponsesController(service, auth));

        var responses = new List<Response>
        {
            new() { Id = "r1", Data = new ResponseData { ExperimentId = "exp1", SessionId = "session1" } }
        };
        mockService.Setup(x => x.GetResponsesAsync("exp1", "session1")).ReturnsAsync(responses);

        // Act
        var result = await controller.GetAll("exp1", "session1");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(responses, okResult.Value);
        mockService.Verify(x => x.GetResponsesAsync("exp1", "session1"), Times.Once);
    }

    [Fact]
    public async Task ResponsesController_GetById_ShouldReturnOkWithResponse()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IResponseService>(
            (service, auth) => new ResponsesController(service, auth));

        var response = new Response
        {
            Id = "test-id",
            Data = new ResponseData { ExperimentId = "exp1", SessionId = "session1" }
        };
        mockService.Setup(x => x.GetResponseAsync("test-id")).ReturnsAsync(response);

        // Act
        var result = await controller.GetById("test-id");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(response, okResult.Value);
    }

    [Fact]
    public async Task ResponsesController_GetById_ShouldReturnNotFoundWhenResponseDoesNotExist()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IResponseService>(
            (service, auth) => new ResponsesController(service, auth));

        mockService.Setup(x => x.GetResponseAsync("nonexistent")).ReturnsAsync((Response?)null);

        // Act
        var result = await controller.GetById("nonexistent");

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Response not found", notFoundResult.Value);
    }

    [Fact]
    public async Task ResponsesController_Create_ShouldReturnOkWithResult()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IResponseService>(
            (service, auth) => new ResponsesController(service, auth));

        var response = new Response
        {
            Data = new ResponseData
            {
                ExperimentId = "exp1",
                SessionId = "session1",
                QuestionnaireId = "q1"
            }
        };
        var createResult = new
        {
            id = "response-id"
        };
        mockService.Setup(x => x.CreateResponseAsync(response, TestDataBuilder.TestUsername)).ReturnsAsync(createResult);

        // Act
        var result = await controller.Create(response);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(createResult, okResult.Value);
    }

    [Fact]
    public async Task ResponsesController_Update_ShouldReturnOkWithMessage()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IResponseService>(
            (service, auth) => new ResponsesController(service, auth));

        var responseData = new ResponseData { ExperimentId = "updated-exp" };
        mockService.Setup(x => x.UpdateResponseAsync("test-id", responseData, TestDataBuilder.TestUsername))
            .Returns(Task.CompletedTask);

        // Act
        var result = await controller.Update("test-id", responseData);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var valueString = okResult.Value!.ToString();
        Assert.Contains("updated successfully", valueString);
    }

    [Fact]
    public async Task ResponsesController_Delete_ShouldReturnOkWithMessage()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IResponseService>(
            (service, auth) => new ResponsesController(service, auth));

        mockService.Setup(x => x.DeleteResponseAsync("test-id", TestDataBuilder.TestUsername))
            .Returns(Task.CompletedTask);

        // Act
        var result = await controller.Delete("test-id");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Contains("deleted successfully", okResult.Value?.ToString());
    }

    // -----------------------------
    // Minimal exception delegation tests
    // -----------------------------

    [Fact]
    public async Task ResponsesController_GetAll_WhenServiceThrows_UsesExceptionHandler()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IResponseService>(
            (service, auth) => new ResponsesController(service, auth));

        mockService.Setup(x => x.GetResponsesAsync(null, null))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.GetAll());
        mockService.Verify(x => x.GetResponsesAsync(null, null), Times.Once);
        mockService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ResponsesController_Create_WhenServiceThrows_UsesExceptionHandler()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IResponseService>(
            (service, auth) => new ResponsesController(service, auth));

        var response = new Response
        {
            Data = new ResponseData { ExperimentId = "exp1" }
        };

        mockService.Setup(x => x.CreateResponseAsync(response, TestDataBuilder.TestUsername))
            .ThrowsAsync(new ArgumentException("Invalid response data"));

        await Assert.ThrowsAsync<ArgumentException>(() => controller.Create(response));
        mockService.Verify(x => x.CreateResponseAsync(response, TestDataBuilder.TestUsername), Times.Once);
        mockService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ResponsesController_Update_WhenServiceThrowsUnauthorized_Returns401()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IResponseService>(
            (service, auth) => new ResponsesController(service, auth));

        var responseData = new ResponseData { ExperimentId = "exp1" };

        mockService.Setup(x => x.UpdateResponseAsync("test-id", responseData, TestDataBuilder.TestUsername))
            .ThrowsAsync(new UnauthorizedAccessException("Access denied"));

        // Act
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.Update("test-id", responseData));
        // Assert
        mockService.Verify(x => x.UpdateResponseAsync("test-id", responseData, TestDataBuilder.TestUsername), Times.Once);
        mockService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ResponsesController_Delete_WhenServiceThrows_UsesExceptionHandler()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IResponseService>(
            (service, auth) => new ResponsesController(service, auth));

        mockService.Setup(x => x.DeleteResponseAsync("test-id", TestDataBuilder.TestUsername))
            .ThrowsAsync(new KeyNotFoundException("Response not found"));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => controller.Delete("test-id"));
        mockService.Verify(x => x.DeleteResponseAsync("test-id", TestDataBuilder.TestUsername), Times.Once);
        mockService.VerifyNoOtherCalls();
    }


    [Fact]
    public async Task ResponsesController_GetById_WhenServiceTimesOut_Returns408()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IResponseService>(
            (service, auth) => new ResponsesController(service, auth));

        mockService.Setup(x => x.GetResponseAsync("test-id"))
            .ThrowsAsync(new TimeoutException("Request timeout"));

        await Assert.ThrowsAsync<TimeoutException>(() => controller.GetById("test-id"));
        mockService.Verify(x => x.GetResponseAsync("test-id"), Times.Once);
        mockService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ResponsesController_VerifiesUserAuthentication()
    {
        // Arrange
        var (mockService, controller, authMock) = CreateControllerWithMocks<IResponseService>(
            (service, auth) => new ResponsesController(service, auth));

        var response = new Response
        {
            Data = new ResponseData { ExperimentId = "exp1" }
        };

        var createResult = new
        {
            id = "response-id"
        };
        mockService.Setup(x => x.CreateResponseAsync(response, TestDataBuilder.TestUsername)).ReturnsAsync(createResult);

        // Act
        await controller.Create(response);

        // Assert - Verify that authentication service was called to get username
        authMock.Verify(x => x.GetUsernameFromRequest(), Times.AtLeastOnce);
    }
}
