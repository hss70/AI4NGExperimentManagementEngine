using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using AI4NGResponsesLambda.Controllers;
using AI4NGResponsesLambda.Interfaces;
using AI4NGResponsesLambda.Models;
using AI4NGExperimentManagement.Shared;
using AI4NGExperimentManagementTests.Shared;

namespace AI4NGResponses.Tests;


public class ResponsesControllerTests : ControllerTestBase<ResponsesController>
{
    private (Mock<IResponseService> mockService, ResponsesController controller, Mock<IAuthenticationService> authMock) CreateController(bool isLocal = true)
        => CreateControllerWithMocks<IResponseService>((svc, auth) => new ResponsesController(svc, auth), isLocal);

    [Fact]
    public async Task GetResponses_ShouldReturnOk_WithResponses()
    {
        // Arrange
        var (mockService, controller, _) = CreateController();
        var responses = new List<object> { new { id = "test-id", experimentId = "test-experiment" } };
        mockService.Setup(x => x.GetResponsesAsync(null, null)).ReturnsAsync(responses);

        // Act
        var result = await controller.GetAll();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(responses, okResult.Value);
    }

    [Theory]
    [InlineData(true, TestDataBuilder.TestUserId, true)]
    [InlineData(false, TestDataBuilder.NonExistentId, false)]
    public async Task GetResponse_ShouldReturnExpectedResult(bool exists, string id, bool expectOk)
    {
        // Arrange
        var (mockService, controller, _) = CreateController();
        var response = new { id = TestDataBuilder.TestUserId, experimentId = "test-experiment" };
        if (exists)
            mockService.Setup(x => x.GetResponseAsync(id)).ReturnsAsync(response);
        else
            mockService.Setup(x => x.GetResponseAsync(id)).ReturnsAsync((object?)null);

        // Act
        var result = await controller.GetById(id);

        // Assert
        if (expectOk)
        {
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(response, okResult.Value);
        }
        else
        {
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal("Response not found", notFoundResult.Value);
        }
    }

    [Fact]
    public async Task Create_ShouldReturnOk_WhenValid()
    {
        // Arrange
        var (mockService, controller, _) = CreateController();
        var response = new Response
        {
            Data = new ResponseData { ExperimentId = "test-experiment" }
        };
        var expectedResult = new { id = "new-response-id" };
        mockService.Setup(x => x.CreateResponseAsync(response, TestDataBuilder.TestUsername)).ReturnsAsync(expectedResult);

        // Act
        var result = await controller.Create(response);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedResult, okResult.Value);
    }

    [Fact]
    public async Task Update_ShouldReturnOk_WhenValid()
    {
        // Arrange
        using var _ = TestEnvironmentHelper.SetLocalTestingMode();
        var (mockService, controller, _) = CreateController();
        var data = new ResponseData { ExperimentId = "updated-experiment" };
        mockService.Setup(x => x.UpdateResponseAsync(TestDataBuilder.TestUserId, data, TestDataBuilder.TestUsername)).Returns(Task.CompletedTask);

        // Act
        var result = await controller.Update(TestDataBuilder.TestUserId, data);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var responseObj = okResult.Value as dynamic;
        Assert.NotNull(responseObj);
    }

    [Fact]
    public async Task DeleteResponse_ShouldReturnOk_WhenValid()
    {
        // Arrange
        using var _ = TestEnvironmentHelper.SetLocalTestingMode();
        var (mockService, controller, _) = CreateController();
        mockService.Setup(x => x.DeleteResponseAsync(TestDataBuilder.TestUserId, TestDataBuilder.TestUsername)).Returns(Task.CompletedTask);

        // Act
        var result = await controller.Delete(TestDataBuilder.TestUserId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var responseObj = okResult.Value as dynamic;
        Assert.NotNull(responseObj);
    }

    [Fact]
    public async Task GetResponsesByExperiment_ShouldReturnOk_WithFilteredResponses()
    {
        // Arrange
        var (mockService, controller, _) = CreateController();
        var responses = new List<object> { new { id = "test-id", experimentId = "test-experiment" } };
        mockService.Setup(x => x.GetResponsesAsync("test-experiment", null)).ReturnsAsync(responses);

        // Act
        var result = await controller.GetAll("test-experiment");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(responses, okResult.Value);
    }

    [Fact]
    public async Task GetResponsesBySession_ShouldReturnOk_WithFilteredResponses()
    {
        // Arrange
        var (mockService, controller, _) = CreateController();
        var responses = new List<object> { new { id = "test-id", sessionId = "test-session" } };
        mockService.Setup(x => x.GetResponsesAsync("test-experiment", "test-session")).ReturnsAsync(responses);

        // Act
        var result = await controller.GetAll("test-experiment", "test-session");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(responses, okResult.Value);
    }

    [Fact]
    public async Task Create_ShouldReturnUnauthorized_WhenNoAuthInNonLocalMode()
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
    public async Task Update_ShouldReturnUnauthorized_WhenNoAuthInNonLocalMode()
    {
        // Arrange
        var (mockService, controller, authMock) = CreateController(isLocal: false);
        authMock.Setup(x => x.GetUsernameFromRequest()).Throws(new UnauthorizedAccessException("Authorization header is required"));
        var data = new ResponseData();

        // Act
        var result = await controller.Update("test-id", data);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result);
    }
}