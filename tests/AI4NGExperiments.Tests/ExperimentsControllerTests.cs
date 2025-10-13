using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using AI4NGExperimentsLambda.Controllers;
using AI4NGExperimentsLambda.Interfaces;
using AI4NGExperimentsLambda.Models;
using AI4NGExperimentManagement.Shared;
using AI4NGExperimentManagementTests.Shared;

namespace AI4NGExperiments.Tests;


public class ExperimentsControllerTests : ControllerTestBase<ExperimentsController>
{
    private (Mock<IExperimentService> mockService, ExperimentsController controller, Mock<IAuthenticationService> authMock) CreateController(bool isLocal = true)
        => CreateControllerWithMocks<IExperimentService>((svc, auth) => new ExperimentsController(svc, auth), isLocal);

    [Fact]
    public async Task GetExperiments_ShouldReturnOk_WithExperiments()
    {
        // Arrange
        var (mockService, controller, _) = CreateController();
        var experiments = new List<object> { new { id = "test-id", name = "Test Experiment" } };
        mockService.Setup(x => x.GetExperimentsAsync()).ReturnsAsync(experiments);

        // Act
        var result = await controller.GetExperiments();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(experiments, okResult.Value);
    }

    [Theory]
    [InlineData(true, TestDataBuilder.TestUserId, true)]
    [InlineData(false, TestDataBuilder.NonExistentId, false)]
    public async Task GetExperiment_ShouldReturnExpectedResult(bool exists, string id, bool expectOk)
    {
        // Arrange
        var (mockService, controller, _) = CreateController();
        var experiment = new { id = TestDataBuilder.TestUserId, name = "Test Experiment" };
        if (exists)
            mockService.Setup(x => x.GetExperimentAsync(id)).ReturnsAsync(experiment);
        else
            mockService.Setup(x => x.GetExperimentAsync(id)).ReturnsAsync((object?)null);

        // Act
        var result = await controller.GetExperiment(id);

        // Assert
        if (expectOk)
        {
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(experiment, okResult.Value);
        }
        else
        {
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal("Experiment not found", notFoundResult.Value);
        }
    }

    [Fact]
    public async Task GetMyExperiments_ShouldReturnOk_InLocalMode()
    {
        // Arrange
        var (mockService, controller, _) = CreateController();
        var experiments = new List<object> { new { id = "my-experiment", name = "My Experiment" } };
        mockService.Setup(x => x.GetMyExperimentsAsync(TestDataBuilder.TestUsername)).ReturnsAsync(experiments);

        // Act
        var result = await controller.GetMyExperiments();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(experiments, okResult.Value);
    }

    [Fact]
    public async Task UpdateExperiment_ShouldReturnOk_WhenValid()
    {
        // Arrange
        using var _ = TestEnvironmentHelper.SetLocalTestingMode();
        var (mockService, controller, _) = CreateController();
        var data = new ExperimentData { Name = "Updated Experiment" };
        mockService.Setup(x => x.UpdateExperimentAsync(TestDataBuilder.TestUserId, data, TestDataBuilder.TestUsername)).Returns(Task.CompletedTask);

        // Act
        var result = await controller.UpdateExperiment(TestDataBuilder.TestUserId, data);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = okResult.Value as dynamic;
        Assert.NotNull(response);
    }
}