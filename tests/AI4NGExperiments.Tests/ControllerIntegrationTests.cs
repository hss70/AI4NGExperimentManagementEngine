using Xunit;
using Microsoft.AspNetCore.Mvc;
using Moq;
using AI4NGExperimentsLambda.Controllers;
using AI4NGExperimentsLambda.Interfaces;
using AI4NGExperimentsLambda.Models;
using AI4NGExperimentManagement.Shared;
using AI4NGExperimentManagementTests.Shared;

namespace AI4NGExperiments.Tests;

public class ControllerIntegrationTests : ControllerTestBase<ExperimentsController>
{
    [Fact]
    public async Task ExperimentsController_GetAll_ShouldReturnOkWithExperiments()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IExperimentService>(
            (service, auth) => new ExperimentsController(service, auth));

        var experiments = new List<Experiment> { TestDataBuilder.CreateValidExperiment() };
        mockService.Setup(x => x.GetExperimentsAsync()).ReturnsAsync(experiments);

        // Act
        var result = await controller.GetAll();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(experiments, okResult.Value);
    }

    [Fact]
    public async Task ExperimentsController_GetById_ShouldReturnOkWithExperiment()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IExperimentService>(
            (service, auth) => new ExperimentsController(service, auth));

        var experiment = TestDataBuilder.CreateValidExperiment();
        mockService.Setup(x => x.GetExperimentAsync("test-id")).ReturnsAsync(experiment);

        // Act
        var result = await controller.GetById("test-id");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(experiment, okResult.Value);
    }

    [Fact]
    public async Task ExperimentsController_GetById_ShouldReturnNotFoundWhenExperimentDoesNotExist()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IExperimentService>(
            (service, auth) => new ExperimentsController(service, auth));

        mockService.Setup(x => x.GetExperimentAsync("nonexistent")).ReturnsAsync((Experiment?)null);

        // Act
        var result = await controller.GetById("nonexistent");

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Experiment not found", notFoundResult.Value);
    }

    [Fact]
    public async Task ExperimentsController_GetMyExperiments_ShouldReturnUserExperiments()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IExperimentService>(
            (service, auth) => new ExperimentsController(service, auth), isResearcher: false);

        var experiments = new List<Experiment> { TestDataBuilder.CreateValidExperiment() };
        mockService.Setup(x => x.GetMyExperimentsAsync(TestDataBuilder.TestUsername)).ReturnsAsync(experiments);

        // Act
        var result = await controller.GetMyExperiments();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(experiments, okResult.Value);
    }

    [Fact]
    public async Task ExperimentsController_Create_ShouldReturnOkWithResult()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IExperimentService>(
            (service, auth) => new ExperimentsController(service, auth));

        var experiment = TestDataBuilder.CreateValidExperiment();
        var createResult = new { id = "test-id" };
        mockService.Setup(x => x.CreateExperimentAsync(experiment, TestDataBuilder.TestUsername)).ReturnsAsync(createResult);

        // Act
        var result = await controller.Create(experiment);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(createResult, okResult.Value);
    }

    [Fact]
    public async Task ExperimentsController_Update_ShouldReturnOkWithMessage()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IExperimentService>(
            (service, auth) => new ExperimentsController(service, auth));

        var updateData = new ExperimentData { Name = "Updated Experiment" };
        mockService.Setup(x => x.UpdateExperimentAsync("test-id", updateData, TestDataBuilder.TestUsername)).Returns(Task.CompletedTask);

        // Act
        var result = await controller.Update("test-id", updateData);

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
        var (mockService, controller, _) = CreateControllerWithMocks<IExperimentService>(
            (service, auth) => new ExperimentsController(service, auth));

        mockService.Setup(x => x.DeleteExperimentAsync("test-id", TestDataBuilder.TestUsername)).Returns(Task.CompletedTask);

        // Act
        var result = await controller.Delete("test-id");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = okResult.Value;
        Assert.NotNull(response);
        Assert.Contains("deleted successfully", response.ToString());
    }

    [Fact]
    public async Task ExperimentsController_Sync_ShouldReturnOkWithSyncResult()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IExperimentService>(
            (service, auth) => new ExperimentsController(service, auth), isResearcher: false);

        var syncResult = new { lastSyncTime = DateTime.UtcNow };
        var lastSyncTime = DateTime.UtcNow.AddHours(-1);
        mockService.Setup(x => x.SyncExperimentAsync("test-id", lastSyncTime, TestDataBuilder.TestUsername)).ReturnsAsync(syncResult);

        // Act
        var result = await controller.Sync("test-id", lastSyncTime);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(syncResult, okResult.Value);
    }

    [Fact]
    public async Task ExperimentsController_GetMembers_ShouldReturnOkWithMembers()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IExperimentService>(
            (service, auth) => new ExperimentsController(service, auth));

        var members = new List<object> { new { userSub = "user123", role = "participant" } };
        mockService.Setup(x => x.GetExperimentMembersAsync("test-id", null, null, null)).ReturnsAsync(members);

        // Act
        var result = await controller.GetMembers("test-id");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(members, okResult.Value);
    }

    [Fact]
    public async Task ExperimentsController_AddMember_ShouldReturnOkWithMessage()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IExperimentService>(
            (service, auth) => new ExperimentsController(service, auth));

        var memberData = new MemberRequest { Role = "participant" };
        mockService.Setup(x => x.AddMemberAsync("test-id", "user123", memberData, TestDataBuilder.TestUsername)).Returns(Task.CompletedTask);

        // Act
        var result = await controller.AddMember("test-id", "user123", memberData);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = okResult.Value;
        Assert.NotNull(response);
        Assert.Contains("added successfully", response.ToString());
    }

    [Fact]
    public async Task ExperimentsController_RemoveMember_ShouldReturnOkWithMessage()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IExperimentService>(
            (service, auth) => new ExperimentsController(service, auth));

        mockService.Setup(x => x.RemoveMemberAsync("test-id", "user123", TestDataBuilder.TestUsername)).Returns(Task.CompletedTask);

        // Act
        var result = await controller.RemoveMember("test-id", "user123");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = okResult.Value;
        Assert.NotNull(response);
        Assert.Contains("removed successfully", response.ToString());
    }

    [Fact]
    public async Task ExperimentsController_GetSessions_ShouldReturnOkWithSessions()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IExperimentService>(
            (service, auth) => new ExperimentsController(service, auth));

        var sessions = new List<object> { new { sessionId = "session1", name = "Test Session" } };
        mockService.Setup(x => x.GetExperimentSessionsAsync("test-id")).ReturnsAsync(sessions);

        // Act
        var result = await controller.GetSessions("test-id");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(sessions, okResult.Value);
    }

    [Fact]
    public async Task ExperimentsController_GetSession_ShouldReturnOkWithSession()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IExperimentService>(
            (service, auth) => new ExperimentsController(service, auth));

        var session = new { sessionId = "session1", name = "Test Session" };
        mockService.Setup(x => x.GetSessionAsync("test-id", "session1")).ReturnsAsync(session);

        // Act
        var result = await controller.GetSession("test-id", "session1");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(session, okResult.Value);
    }

    [Fact]
    public async Task ExperimentsController_GetSession_ShouldReturnNotFoundWhenSessionDoesNotExist()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IExperimentService>(
            (service, auth) => new ExperimentsController(service, auth));

        mockService.Setup(x => x.GetSessionAsync("test-id", "nonexistent")).ReturnsAsync((object?)null);

        // Act
        var result = await controller.GetSession("test-id", "nonexistent");

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Session not found", notFoundResult.Value);
    }

    [Fact]
    public async Task ExperimentsController_CreateSession_ShouldReturnOkWithResult()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IExperimentService>(
            (service, auth) => new ExperimentsController(service, auth));

        var request = new CreateSessionRequest { SessionName = "Test Session" };
        var createResult = new { sessionId = "session1" };
        mockService.Setup(x => x.CreateSessionAsync("test-id", It.IsAny<CreateSessionRequest>(), TestDataBuilder.TestUsername)).ReturnsAsync(createResult);

        // Act
        var result = await controller.CreateSession("test-id", request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(createResult, okResult.Value);
    }

    [Fact]
    public async Task ExperimentsController_UpdateSession_ShouldReturnOkWithMessage()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IExperimentService>(
            (service, auth) => new ExperimentsController(service, auth));

        var sessionData = new SessionData { SessionName = "Updated Session" };
        mockService.Setup(x => x.UpdateSessionAsync("test-id", "session1", sessionData, TestDataBuilder.TestUsername)).Returns(Task.CompletedTask);

        // Act
        var result = await controller.UpdateSession("test-id", "session1", sessionData);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = okResult.Value;
        Assert.NotNull(response);
        Assert.Contains("updated successfully", response.ToString());
    }

    [Fact]
    public async Task ExperimentsController_DeleteSession_ShouldReturnOkWithMessage()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IExperimentService>(
            (service, auth) => new ExperimentsController(service, auth));

        mockService.Setup(x => x.DeleteSessionAsync("test-id", "session1", TestDataBuilder.TestUsername)).Returns(Task.CompletedTask);

        // Act
        var result = await controller.DeleteSession("test-id", "session1");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = okResult.Value;
        Assert.NotNull(response);
        Assert.Contains("deleted successfully", response.ToString());
    }


}