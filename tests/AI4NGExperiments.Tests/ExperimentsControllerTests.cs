using Moq;
using Microsoft.AspNetCore.Mvc;
using AI4NGExperimentsLambda.Controllers;
using ResearcherExperimentsController = AI4NGExperimentsLambda.Controllers.Researcher.ExperimentsController;
using AI4NGExperimentsLambda.Models;
using AI4NGExperimentManagement.Shared;
using AI4NGExperimentManagementTests.Shared;
using AI4NGExperimentsLambda.Models.Dtos;
using AI4NGExperimentsLambda.Interfaces.Researcher;

namespace AI4NGExperiments.Tests;


public class ExperimentsControllerTests : ControllerTestBase<ResearcherExperimentsController>
{
    private (Mock<IExperimentsService> mockService, ResearcherExperimentsController controller, Mock<IAuthenticationService> authMock) CreateController(bool isLocal = true)
        => CreateControllerWithMocks<IExperimentsService>((svc, auth) => new ResearcherExperimentsController(svc, auth), isLocal);

    [Theory]
    [InlineData(true, TestDataBuilder.TestUserId, true)]
    [InlineData(false, TestDataBuilder.NonExistentId, false)]
    public async Task GetById_ShouldReturnExpectedResult(bool exists, string id, bool expectOk)
    {
        // Arrange
        var (mockService, controller, _) = CreateController();
        var experiment = new AI4NGExperimentsLambda.Models.Dtos.ExperimentDto { Id = TestDataBuilder.TestUserId, Data = new ExperimentData { Name = "Test Experiment" }, QuestionnaireConfig = new QuestionnaireConfig() };
        if (exists)
            mockService.Setup(x => x.GetExperimentAsync(id, It.IsAny<System.Threading.CancellationToken>())).ReturnsAsync(experiment);
        else
            mockService.Setup(x => x.GetExperimentAsync(id, It.IsAny<System.Threading.CancellationToken>())).ReturnsAsync((AI4NGExperimentsLambda.Models.Dtos.ExperimentDto?)null);

        // Act
        var result = await controller.GetById(id, System.Threading.CancellationToken.None);

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

    [Fact(Skip = "Refactor: moved to LegacyMonolith - session/member/sync tests quarantined")]
    public async Task GetMyExperiments_ShouldReturnOk_InLocalMode()
    {
        // Arrange
        // Arrange - participant endpoint moved to ParticipantExperimentsController (stubbed)
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
    public async Task UpdateExperiment_ShouldReturnOk_WhenValid()
    {
        // Arrange
        using var _ = TestEnvironmentHelper.SetLocalTestingMode();
        var (mockService, controller, _) = CreateController();
        var data = new ExperimentData { Name = "Updated Experiment" };
        mockService.Setup(x => x.UpdateExperimentAsync(TestDataBuilder.TestUserId, data, TestDataBuilder.TestUsername, It.IsAny<System.Threading.CancellationToken>())).Returns(System.Threading.Tasks.Task.CompletedTask);

        // Act
        var result = await controller.Update(TestDataBuilder.TestUserId, data, System.Threading.CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = okResult.Value as dynamic;
        Assert.NotNull(response);
    }

    [Fact(Skip = "Refactor: moved to LegacyMonolith - session/member/sync tests quarantined")]
    public async Task GetByIdMembers_ShouldReturnOk_WithMembers()
    {
        // Arrange
        var authMock = CreateAuthMock();
        var controller = new AI4NGExperimentsLambda.Controllers.Researcher.ExperimentParticipantsController(authMock.Object);
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };

        // Act (controller is currently stubbed)
        var result = controller.List(TestDataBuilder.TestUserId, null, null, null);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var body = ok.Value as dynamic;
        Assert.NotNull(body);
        Assert.Equal(TestDataBuilder.TestUserId, (string)body.experimentId);
    }

    [Fact(Skip = "Refactor: moved to LegacyMonolith - session/member/sync tests quarantined")]
    public async Task AddMember_ShouldReturnOk_WhenValid()
    {
        // Arrange - participant endpoints are now in ExperimentParticipantsController (stubbed)
        var authMock = CreateAuthMock();
        var controller = new AI4NGExperimentsLambda.Controllers.Researcher.ExperimentParticipantsController(authMock.Object);
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };
        var member = new MemberRequest { Role = "participant" };

        // Act
        var result = controller.Upsert(TestDataBuilder.TestUserId, TestDataBuilder.TestUserId, member);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var body = ok.Value as dynamic;
        Assert.NotNull(body);
    }

    [Fact]
    public async Task AddMember_ShouldReturnUnauthorized_WhenAuthFails()
    {
        // Arrange - unauthorized researcher should be forbidden
        var authMock = CreateAuthMock(isResearcher: false);
        var controller = new AI4NGExperimentsLambda.Controllers.Researcher.ExperimentParticipantsController(authMock.Object);
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };
        var member = new MemberRequest { Role = "participant" };

        // Act & Assert - RequireResearcher now throws ForbiddenException for non-researchers
        Assert.Throws<AI4NGExperimentManagement.Shared.ForbiddenException>(() => controller.Upsert(TestDataBuilder.TestUserId, TestDataBuilder.TestUserId, member));
    }

    [Fact(Skip = "Refactor: moved to LegacyMonolith - session/member/sync tests quarantined")]
    public async Task RemoveMember_ShouldReturnOk_WhenValid()
    {
        // Arrange - use participant controller stub
        var authMock = CreateAuthMock();
        var controller = new AI4NGExperimentsLambda.Controllers.Researcher.ExperimentParticipantsController(authMock.Object);
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };

        // Act
        var result = controller.Remove(TestDataBuilder.TestUserId, TestDataBuilder.NonExistentId);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var body = ok.Value as dynamic;
        Assert.NotNull(body);
    }

    [Fact]
    public async Task RemoveMember_ShouldReturnUnauthorized_WhenAuthFails()
    {
        // Arrange - non-researcher should be forbidden
        var authMock = CreateAuthMock(isResearcher: false);
        var controller = new AI4NGExperimentsLambda.Controllers.Researcher.ExperimentParticipantsController(authMock.Object);
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };

        // Act & Assert
        Assert.Throws<AI4NGExperimentManagement.Shared.ForbiddenException>(() => controller.Remove(TestDataBuilder.TestUserId, TestDataBuilder.NonExistentId));
    }
}