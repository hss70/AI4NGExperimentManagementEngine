using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using AI4NGQuestionnairesLambda.Controllers;
using AI4NGQuestionnairesLambda.Interfaces;
using AI4NGQuestionnairesLambda.Models;
using AI4NGExperimentManagement.Shared;
using AI4NGExperimentManagementTests.Shared;

namespace AI4NGQuestionnaires.Tests;


[Collection("QuestionnairesCollection")]
public class QuestionnairesControllerTests : ControllerTestBase<QuestionnairesController>, IDisposable
{
    private readonly string? _originalEndpointUrl;

    public QuestionnairesControllerTests()
    {
        _originalEndpointUrl = Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("AWS_ENDPOINT_URL", _originalEndpointUrl);
    }

    private (Mock<IQuestionnaireService> mockService, QuestionnairesController controller, Mock<IAuthenticationService> authMock) CreateController(bool isLocal = true, bool isResearcher = true)
        => CreateControllerWithMocks<IQuestionnaireService>((svc, auth) => new QuestionnairesController(svc, auth), isLocal, isResearcher);

    [Fact]
    public async Task GetAll_ShouldReturnOk_WithQuestionnaires()
    {
        // Arrange
        var (mockService, controller, auth) = CreateController();
        var questionnaires = new List<Questionnaire>
        {
            new() { Id = "test-1", Data = new QuestionnaireData { Name = "Test 1" } }
        };
        mockService.Setup(x => x.GetAllAsync()).ReturnsAsync(questionnaires);

        // Act
        var result = await controller.GetAll();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(questionnaires, okResult.Value);
    }

    [Fact]
    public async Task Create_ShouldReturnOk_WhenValidRequest()
    {
        // Arrange
        var (mockService, controller, auth) = CreateController();
        var request = new CreateQuestionnaireRequest
        {
            Id = "test-id",
            Data = new QuestionnaireData { Name = "Test" }
        };
        mockService.Setup(x => x.CreateAsync(request, TestDataBuilder.TestUsername)).ReturnsAsync(TestDataBuilder.TestUserId);
        controller.HttpContext.Request.Path = TestDataBuilder.Paths.ResearcherQuestionnaires;

        // Act
        var result = await controller.Create(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsAssignableFrom<object>(okResult.Value);
        Assert.NotNull(response);
    }

    [Theory]
    [InlineData(true, TestDataBuilder.TestUserId, true)]
    [InlineData(false, TestDataBuilder.NonExistentId, false)]
    public async Task GetById_ShouldReturnExpectedResult(bool exists, string id, bool expectOk)
    {
        // Arrange
        var (mockService, controller, auth) = CreateController();
        var questionnaire = new Questionnaire { Id = TestDataBuilder.TestUserId, Data = new QuestionnaireData { Name = "Test" } };
        if (exists)
            mockService.Setup(x => x.GetByIdAsync(id)).ReturnsAsync(questionnaire);
        else
            mockService.Setup(x => x.GetByIdAsync(id)).ReturnsAsync((Questionnaire?)null);

        // Act
        var result = await controller.GetById(id);

        // Assert
        if (expectOk)
        {
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(questionnaire, okResult.Value);
        }
        else
        {
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
            Assert.Equal("Questionnaire not found", notFoundResult.Value);
        }
    }

    [Fact]
    public async Task Update_ShouldReturnOk_WhenValid()
    {
        // Arrange
        using var _ = TestEnvironmentHelper.SetLocalTestingMode();

        var (mockService, controller, auth) = CreateController();
        var data = new QuestionnaireData { Name = "Updated Questionnaire" };
        mockService.Setup(x => x.UpdateAsync(TestDataBuilder.TestUserId, data, TestDataBuilder.TestUsername)).Returns(Task.CompletedTask);
        controller.HttpContext.Request.Path = TestDataBuilder.Paths.ResearcherQuestionnaires;

        // Act
        var result = await controller.Update(TestDataBuilder.TestUserId, data);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = okResult.Value as dynamic;
        Assert.NotNull(response);
    }

    [Fact]
    public async Task Delete_ShouldReturnOk_WhenValid()
    {
        // Arrange
        using var _ = TestEnvironmentHelper.SetLocalTestingMode();

        var (mockService, controller, auth) = CreateController();
        mockService.Setup(x => x.DeleteAsync(TestDataBuilder.TestUserId, TestDataBuilder.TestUsername)).Returns(Task.CompletedTask);
        controller.HttpContext.Request.Path = TestDataBuilder.Paths.ResearcherQuestionnaires;

        // Act
        var result = await controller.Delete(TestDataBuilder.TestUserId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = okResult.Value as dynamic;
        Assert.NotNull(response);
    }

    [Fact]
    public async Task Create_ShouldReturnForbidden_WhenNotResearcher()
    {
        // Arrange
        var (mockService, controller, _) = CreateController(isResearcher: false);
        var request = new CreateQuestionnaireRequest { Id = "test", Data = new QuestionnaireData { Name = "Test" } };
        controller.HttpContext.Request.Path = TestDataBuilder.Paths.ParticipantQuestionnaires;

        // Act
        var result = await controller.Create(request);

        // Assert
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task GetUsernameFromJwt_ShouldReturnTestUser_InLocalMode()
    {
        // Arrange
        var (mockService, controller, auth) = CreateController();
        var request = new CreateQuestionnaireRequest { Id = "test", Data = new QuestionnaireData { Name = "Test" } };
        mockService.Setup(x => x.CreateAsync(request, TestDataBuilder.TestUsername)).ReturnsAsync("test");
        controller.HttpContext.Request.Path = TestDataBuilder.Paths.ResearcherQuestionnaires;

        // Act
        var result = await controller.Create(request);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        mockService.Verify(x => x.CreateAsync(request, TestDataBuilder.TestUsername), Times.Once);
    }

    [Fact]
    public async Task Create_ShouldReturnUnauthorized_WhenNoAuthInNonLocalMode()
    {
        // Arrange
        var (mockService, controller, auth) = CreateController(isLocal: false);
        auth.Setup(x => x.GetUsernameFromRequest()).Throws(new UnauthorizedAccessException("Authorization header is required"));
        controller.ControllerContext.HttpContext.Request.Headers.Clear();
        var request = new CreateQuestionnaireRequest { Id = "test", Data = new QuestionnaireData { Name = "Test" } };

        // Act
        var result = await controller.Create(request);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Update_ShouldReturnUnauthorized_WhenNoAuthInNonLocalMode()
    {
        // Arrange
        var (mockService, controller, auth) = CreateController(isLocal: false);
        auth.Setup(x => x.GetUsernameFromRequest()).Throws(new UnauthorizedAccessException("Authorization header is required"));
        controller.ControllerContext.HttpContext.Request.Headers.Clear();
        var data = new QuestionnaireData { Name = "Test" };

        // Act
        var result = await controller.Update("test-id", data);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Create_ShouldReturnUnauthorized_WhenNoAuthHeader()
    {
        // Arrange
        var (mockService, controller, auth) = CreateController(isLocal: false);
        auth.Setup(x => x.GetUsernameFromRequest()).Throws(new UnauthorizedAccessException("Authorization header is required"));
        controller.ControllerContext.HttpContext.Request.Headers.Clear();
        var request = new CreateQuestionnaireRequest { Id = "test", Data = new QuestionnaireData { Name = "Test" } };

        // Act
        var result = await controller.Create(request);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Create_ShouldReturnUnauthorized_WhenInvalidToken()
    {
        // Arrange
        var (mockService, controller, auth) = CreateController(isLocal: false);
        auth.Setup(x => x.GetUsernameFromRequest()).Throws(new UnauthorizedAccessException("Invalid token format"));
        controller.ControllerContext.HttpContext.Request.Headers.Authorization = "Bearer invalid-token";
        var request = new CreateQuestionnaireRequest { Id = "test", Data = new QuestionnaireData { Name = "Test" } };

        // Act
        var result = await controller.Create(request);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result);
    }
}