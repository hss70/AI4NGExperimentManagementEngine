using Microsoft.AspNetCore.Mvc;
using Moq;
using AI4NGExperimentsLambda.Controllers;
using ResearcherExperimentsController = AI4NGExperimentsLambda.Controllers.Researcher.ExperimentsController;
using AI4NGExperimentsLambda.Interfaces.Researcher;
using AI4NGExperimentsLambda.Models;
using AI4NGExperimentsLambda.Models.Dtos;
using AI4NGExperimentManagementTests.Shared;

namespace AI4NGExperiments.Tests;

public class ControllerIntegrationTests : ControllerTestBase<ResearcherExperimentsController>
{
    [Fact]
    public async Task ExperimentsController_GetAll_ShouldReturnOkWithExperiments()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IExperimentsService>(
            (service, auth) => new ResearcherExperimentsController(service, auth));

        var experiments = new List<AI4NGExperimentsLambda.Models.Dtos.ExperimentListDto> { new AI4NGExperimentsLambda.Models.Dtos.ExperimentListDto { Id = "exp-1", Name = "Test", Description = "Desc" } };
        mockService.Setup(x => x.GetExperimentsAsync(It.IsAny<System.Threading.CancellationToken>())).ReturnsAsync(experiments);

        // Act
        var result = await controller.GetAll(System.Threading.CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(experiments, okResult.Value);
    }

    [Fact]
    public async Task ExperimentsController_GetById_ShouldReturnOkWithExperiment()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IExperimentsService>(
            (service, auth) => new ResearcherExperimentsController(service, auth));

        var experiment = TestDataBuilder.CreateValidExperiment();
        var expDto = new AI4NGExperimentsLambda.Models.Dtos.ExperimentDto
        {
            Id = experiment.Id,
            Data = experiment.Data,
            UpdatedAt = DateTime.UtcNow.ToString("O")
        };
        mockService.Setup(x => x.GetExperimentAsync("test-id", It.IsAny<System.Threading.CancellationToken>())).ReturnsAsync(expDto);

        // Act
        var result = await controller.GetById("test-id", System.Threading.CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<AI4NGExperimentsLambda.Models.Dtos.ExperimentDto>(okResult.Value);
        Assert.Equal(experiment.Id, returned.Id);
        Assert.Equal(experiment.Data.Name, returned.Data.Name);
    }

    [Fact]
    public async Task ExperimentsController_GetById_ShouldReturnNotFoundWhenExperimentDoesNotExist()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IExperimentsService>(
            (service, auth) => new ResearcherExperimentsController(service, auth));

        mockService.Setup(x => x.GetExperimentAsync("nonexistent", It.IsAny<System.Threading.CancellationToken>())).ReturnsAsync((AI4NGExperimentsLambda.Models.Dtos.ExperimentDto?)null);

        // Act
        var result = await controller.GetById("nonexistent", System.Threading.CancellationToken.None);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Experiment not found", notFoundResult.Value);
    }

    [Fact]
    public async Task ExperimentsController_Create_ShouldReturnOkWithResult()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IExperimentsService>(
            (service, auth) => new ResearcherExperimentsController(service, auth));

        var experiment = TestDataBuilder.CreateValidExperiment();
        var createResult = new AI4NGExperimentsLambda.Models.Dtos.IdResponseDto { Id = "test-id" };
        mockService.Setup(x => x.CreateExperimentAsync(experiment, TestDataBuilder.TestUsername, It.IsAny<System.Threading.CancellationToken>())).ReturnsAsync(createResult);

        // Act
        var result = await controller.Create(experiment, System.Threading.CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(createResult, okResult.Value);
    }

    [Fact]
    public async Task ExperimentsController_Update_ShouldReturnOkWithMessage()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IExperimentsService>(
            (service, auth) => new ResearcherExperimentsController(service, auth));

        var updateData = new ExperimentData { Name = "Updated Experiment" };
        mockService.Setup(x => x.UpdateExperimentAsync("test-id", updateData, TestDataBuilder.TestUsername, It.IsAny<System.Threading.CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        var result = await controller.Update("test-id", updateData, System.Threading.CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = okResult.Value;
        Assert.NotNull(response);
        Assert.Contains("updated successfully", response.ToString());
    }

    [Fact]
    public async Task ExperimentsController_Delete_ShouldReturnOkWithMessage()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IExperimentsService>(
            (service, auth) => new ResearcherExperimentsController(service, auth));

        mockService.Setup(x => x.DeleteExperimentAsync("test-id", TestDataBuilder.TestUsername, It.IsAny<System.Threading.CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        var result = await controller.Delete("test-id", System.Threading.CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = okResult.Value;
        Assert.NotNull(response);
        Assert.Contains("deleted successfully", response.ToString());
    }

}
