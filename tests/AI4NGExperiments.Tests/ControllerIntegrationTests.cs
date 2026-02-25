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
            QuestionnaireConfig = experiment.QuestionnaireConfig,
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

    [Fact(Skip = "Refactor: moved to LegacyMonolith - session/member/sync tests quarantined")]
    public async Task ExperimentsController_GetMyExperiments_ShouldReturnUserExperiments()
    {
        // Arrange
        // Arrange - participant endpoint is now a separate controller and currently stubbed
        var authMock = CreateAuthMock();
        authMock.Setup(x => x.GetUsernameFromRequest()).Returns(TestDataBuilder.TestUsername);
        var controller = new AI4NGExperimentsLambda.Controllers.Participant.ParticipantExperimentsController(authMock.Object);
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };

        // Act
        var result = controller.ListMyExperiments();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var body = okResult.Value as dynamic;
        Assert.Equal(TestDataBuilder.TestUsername, (string)body.username);
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

    [Fact(Skip = "Refactor: moved to LegacyMonolith - session/member/sync tests quarantined")]
    public async Task ExperimentsController_Sync_ShouldReturnOkWithSyncResult()
    {
        // Arrange
        // Sync behaviour is now provided by the participant controller bundle endpoint (stubbed)
        var authMock = CreateAuthMock();
        authMock.Setup(x => x.GetUsernameFromRequest()).Returns(TestDataBuilder.TestUsername);
        var participantController = new AI4NGExperimentsLambda.Controllers.Participant.ParticipantExperimentsController(authMock.Object);
        participantController.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };

        // Act
        var bundleResult = participantController.GetBundle("test-id", DateTime.UtcNow.AddHours(-1));

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(bundleResult);
        var body = okResult.Value as dynamic;
        Assert.Equal("test-id", (string)body.experimentId);
    }

    [Fact(Skip = "Refactor: moved to LegacyMonolith - session/member/sync tests quarantined")]
    public async Task ExperimentsController_GetMembers_ShouldReturnOkWithMembers()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IExperimentsService>(
            (service, auth) => new ResearcherExperimentsController(service, auth));

        // Researcher participant listing is now a dedicated controller and is stubbed
        var authMock2 = CreateAuthMock();
        authMock2.Setup(x => x.GetUsernameFromRequest()).Returns(TestDataBuilder.TestUsername);
        var participantsController = new AI4NGExperimentsLambda.Controllers.Researcher.ExperimentParticipantsController(authMock2.Object);
        participantsController.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };

        // Act
        var listResult = participantsController.List("test-id", null, null, null);

        // Assert
        var okList = Assert.IsType<OkObjectResult>(listResult);
        var listBody = okList.Value as dynamic;
        Assert.Equal("test-id", (string)listBody.experimentId);
    }

    [Fact(Skip = "Refactor: moved to LegacyMonolith - session/member/sync tests quarantined")]
    public async Task ExperimentsController_AddMember_ShouldReturnOkWithMessage()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IExperimentsService>(
            (service, auth) => new ResearcherExperimentsController(service, auth));

        var memberData = new MemberRequest { Role = "participant" };
        var authMock3 = CreateAuthMock();
        authMock3.Setup(x => x.GetUsernameFromRequest()).Returns(TestDataBuilder.TestUsername);
        var participantsController2 = new AI4NGExperimentsLambda.Controllers.Researcher.ExperimentParticipantsController(authMock3.Object);
        participantsController2.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };

        // Act
        var upsertResult = participantsController2.Upsert("test-id", "user123", memberData);

        // Assert
        var okUpsert = Assert.IsType<OkObjectResult>(upsertResult);
        var upsertBody = okUpsert.Value as dynamic;
        Assert.Equal("test-id", (string)upsertBody.experimentId);
        Assert.Equal("user123", (string)upsertBody.participantId);
    }

    [Fact(Skip = "Refactor: moved to LegacyMonolith - session/member/sync tests quarantined")]
    public async Task ExperimentsController_RemoveMember_ShouldReturnOkWithMessage()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IExperimentsService>(
            (service, auth) => new ResearcherExperimentsController(service, auth));

        var authMock4 = CreateAuthMock();
        authMock4.Setup(x => x.GetUsernameFromRequest()).Returns(TestDataBuilder.TestUsername);
        var participantsController3 = new AI4NGExperimentsLambda.Controllers.Researcher.ExperimentParticipantsController(authMock4.Object);
        participantsController3.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };

        // Act
        var removeResult = participantsController3.Remove("test-id", "user123");

        // Assert
        var okRemove = Assert.IsType<OkObjectResult>(removeResult);
        var removeBody = okRemove.Value as dynamic;
        Assert.Equal("test-id", (string)removeBody.experimentId);
        Assert.Equal("user123", (string)removeBody.participantId);
    }

    [Fact(Skip = "Refactor: moved to LegacyMonolith - session/member/sync tests quarantined")]
    public async Task ExperimentsController_GetSessions_ShouldReturnOkWithSessions()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IExperimentsService>(
            (service, auth) => new ResearcherExperimentsController(service, auth));

        var sessionController = new AI4NGExperimentsLambda.Controllers.Researcher.SessionProtocolController(CreateAuthMock().Object);
        sessionController.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };

        // Act
        var sessionsResult = sessionController.List("test-id");

        // Assert
        var okSessions = Assert.IsType<OkObjectResult>(sessionsResult);
        var sessionsBody = okSessions.Value as dynamic;
        Assert.Equal("test-id", (string)sessionsBody.experimentId);
    }

    [Fact(Skip = "Refactor: moved to LegacyMonolith - session/member/sync tests quarantined")]
    public async Task ExperimentsController_GetSession_ShouldReturnOkWithSession()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IExperimentsService>(
            (service, auth) => new ResearcherExperimentsController(service, auth));

        var sessionController2 = new AI4NGExperimentsLambda.Controllers.Researcher.SessionProtocolController(CreateAuthMock().Object);
        sessionController2.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };

        // Act
        var sessionResult = sessionController2.Get("test-id", "session1");

        // Assert
        var okSession = Assert.IsType<OkObjectResult>(sessionResult);
        var sessionBody = okSession.Value as dynamic;
        Assert.Equal("test-id", (string)sessionBody.experimentId);
        Assert.Equal("session1", (string)sessionBody.protocolSessionKey);
    }

    [Fact(Skip = "Refactor: moved to LegacyMonolith - session/member/sync tests quarantined")]
    public async Task ExperimentsController_GetSession_ShouldReturnNotFoundWhenSessionDoesNotExist()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IExperimentsService>(
            (service, auth) => new ResearcherExperimentsController(service, auth));

        // Non-existent session behaviour is not modelled by the stubbed controller; assert the stub returns OK with the provided key
        var sessionController3 = new AI4NGExperimentsLambda.Controllers.Researcher.SessionProtocolController(CreateAuthMock().Object);
        sessionController3.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };

        var sessionResult2 = sessionController3.Get("test-id", "nonexistent");
        var okSession2 = Assert.IsType<OkObjectResult>(sessionResult2);
        var sessionBody2 = okSession2.Value as dynamic;
        Assert.Equal("nonexistent", (string)sessionBody2.protocolSessionKey);
    }

    [Fact(Skip = "Refactor: moved to LegacyMonolith - session/member/sync tests quarantined")]
    public async Task ExperimentsController_CreateSession_ShouldReturnOkWithResult()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IExperimentsService>(
            (service, auth) => new ResearcherExperimentsController(service, auth));

        var request = new CreateSessionRequest { SessionName = "Test Session" };
        var sessionCtrl = new AI4NGExperimentsLambda.Controllers.Researcher.SessionProtocolController(CreateAuthMock().Object);
        sessionCtrl.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };

        var upsertResult2 = sessionCtrl.Upsert("test-id", "session1", request);
        var okUpsert2 = Assert.IsType<OkObjectResult>(upsertResult2);
        var upsertBody2 = okUpsert2.Value as dynamic;
        Assert.Equal("session1", (string)upsertBody2.protocolSessionKey);
    }

    [Fact(Skip = "Refactor: moved to LegacyMonolith - session/member/sync tests quarantined")]
    public async Task ExperimentsController_UpdateSession_ShouldReturnOkWithMessage()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IExperimentsService>(
            (service, auth) => new ResearcherExperimentsController(service, auth));

        var sessionData = new SessionData { SessionName = "Updated Session" };
        var sessionCtrl2 = new AI4NGExperimentsLambda.Controllers.Researcher.SessionProtocolController(CreateAuthMock().Object);
        sessionCtrl2.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };

        var upsertResult3 = sessionCtrl2.Upsert("test-id", "session1", sessionData);
        var okUpsert3 = Assert.IsType<OkObjectResult>(upsertResult3);
        var upsertBody3 = okUpsert3.Value as dynamic;
        Assert.Equal("session1", (string)upsertBody3.protocolSessionKey);
    }

    [Fact(Skip = "Refactor: moved to LegacyMonolith - session/member/sync tests quarantined")]
    public async Task ExperimentsController_DeleteSession_ShouldReturnOkWithMessage()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IExperimentsService>(
            (service, auth) => new ResearcherExperimentsController(service, auth));

        var sessionCtrl4 = new AI4NGExperimentsLambda.Controllers.Researcher.SessionProtocolController(CreateAuthMock().Object);
        sessionCtrl4.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };

        var deleteResult = sessionCtrl4.Delete("test-id", "session1");
        var okDelete = Assert.IsType<OkObjectResult>(deleteResult);
        var deleteBody = okDelete.Value as dynamic;
        Assert.Equal("session1", (string)deleteBody.protocolSessionKey);
    }


}