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

    [Theory]
    [InlineData(true, TestDataBuilder.TestUserId, true)]
    [InlineData(false, TestDataBuilder.NonExistentId, false)]
    public async Task GetById_ShouldReturnExpectedResult(bool exists, string id, bool expectOk)
    {
        // Arrange
        var (mockService, controller, _) = CreateController();
        var experiment = new { id = TestDataBuilder.TestUserId, name = "Test Experiment" };
        if (exists)
            mockService.Setup(x => x.GetExperimentAsync(id)).ReturnsAsync(experiment);
        else
            mockService.Setup(x => x.GetExperimentAsync(id)).ReturnsAsync((object?)null);

        // Act
        var result = await controller.GetById(id);

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
        var result = await controller.Update(TestDataBuilder.TestUserId, data);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = okResult.Value as dynamic;
        Assert.NotNull(response);
    }

    [Fact]
    public async Task GetByIdMembers_ShouldReturnOk_WithMembers()
    {
        // Arrange
        var (mockService, controller, _) = CreateController();
        var members = new List<object>
        {
            new { userSub = "user-1", role = "participant" },
            new { userSub = "user-2", role = "researcher" }
        };
        mockService.Setup(x => x.GetExperimentMembersAsync(TestDataBuilder.TestUserId)).ReturnsAsync(members);

        // Act
        var result = await controller.GetMembers(TestDataBuilder.TestUserId);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(members, ok.Value);
    }

    [Fact]
    public async Task AddMember_ShouldReturnOk_WhenValid()
    {
        // Arrange
        var (mockService, controller, authMock) = CreateController();
        var member = new MemberRequest { Role = "participant" };
        mockService
            .Setup(x => x.AddMemberAsync(TestDataBuilder.TestUserId, TestDataBuilder.TestUserId, member, TestDataBuilder.TestUsername))
            .Returns(Task.CompletedTask);

        // Act
        var result = await controller.AddMember(TestDataBuilder.TestUserId, TestDataBuilder.TestUserId, member);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var body = ok.Value as dynamic;
        Assert.NotNull(body);
        mockService.Verify(x => x.AddMemberAsync(TestDataBuilder.TestUserId, TestDataBuilder.TestUserId, member, TestDataBuilder.TestUsername), Times.Once);
    }

    [Fact]
    public async Task AddMember_ShouldReturnUnauthorized_WhenAuthFails()
    {
        // Arrange
        var (mockService, controller, authMock) = CreateController(isLocal: false);
        authMock.Setup(x => x.GetUsernameFromRequest()).Throws(new UnauthorizedAccessException("Authorization header is required"));
        var member = new MemberRequest { Role = "participant" };

        // Act
        var result = await controller.AddMember(TestDataBuilder.TestUserId, TestDataBuilder.TestUserId, member);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result);
        mockService.Verify(x => x.AddMemberAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<MemberRequest>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RemoveMember_ShouldReturnOk_WhenValid()
    {
        // Arrange
        var (mockService, controller, _) = CreateController();
        mockService
            .Setup(x => x.RemoveMemberAsync(TestDataBuilder.TestUserId, TestDataBuilder.NonExistentId, TestDataBuilder.TestUsername))
            .Returns(Task.CompletedTask);

        // Act
        var result = await controller.RemoveMember(TestDataBuilder.TestUserId, TestDataBuilder.NonExistentId);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var body = ok.Value as dynamic;
        Assert.NotNull(body);
        mockService.Verify(x => x.RemoveMemberAsync(TestDataBuilder.TestUserId, TestDataBuilder.NonExistentId, TestDataBuilder.TestUsername), Times.Once);
    }

    [Fact]
    public async Task RemoveMember_ShouldReturnUnauthorized_WhenAuthFails()
    {
        // Arrange
        var (mockService, controller, authMock) = CreateController(isLocal: false);
        authMock.Setup(x => x.GetUsernameFromRequest()).Throws(new UnauthorizedAccessException("Bearer token required"));

        // Act
        var result = await controller.RemoveMember(TestDataBuilder.TestUserId, TestDataBuilder.NonExistentId);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result);
        mockService.Verify(x => x.RemoveMemberAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}