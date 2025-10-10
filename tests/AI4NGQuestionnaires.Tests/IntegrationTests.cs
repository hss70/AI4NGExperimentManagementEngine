using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using AI4NGQuestionnairesLambda.Controllers;
using AI4NGQuestionnairesLambda.Interfaces;
using AI4NGQuestionnairesLambda.Models;

namespace AI4NGQuestionnaires.Tests;

public class ControllerUnitTests
{
    private readonly Mock<IQuestionnaireService> _mockService;
    private readonly QuestionnairesController _controller;

    public ControllerUnitTests()
    {
        _mockService = new Mock<IQuestionnaireService>();
        _controller = new QuestionnairesController(_mockService.Object);
        
        // Mock HttpContext for local testing
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test"] = "true";
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        
        // Set local testing environment
        Environment.SetEnvironmentVariable("AWS_ENDPOINT_URL", "http://localhost:8000");
    }

    [Fact]
    public async Task GetAll_ShouldReturnOk_WithQuestionnaires()
    {
        // Arrange
        var questionnaires = new List<Questionnaire>
        {
            new() { Id = "test-1", Data = new QuestionnaireData { Name = "Test 1" } }
        };
        _mockService.Setup(x => x.GetAllAsync()).ReturnsAsync(questionnaires);

        // Act
        var result = await _controller.GetAll();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(questionnaires, okResult.Value);
    }

    [Fact]
    public async Task Create_ShouldReturnOk_WhenValidRequest()
    {
        // Arrange
        var request = new CreateQuestionnaireRequest
        {
            Id = "test-id",
            Data = new QuestionnaireData { Name = "Test" }
        };
        _mockService.Setup(x => x.CreateAsync(request, "testuser")).ReturnsAsync("test-id");

        // Mock researcher path
        _controller.HttpContext.Request.Path = "/api/researcher/questionnaires";

        // Act
        var result = await _controller.Create(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsAssignableFrom<object>(okResult.Value);
        Assert.NotNull(response);
    }

    [Fact]
    public async Task GetById_ShouldReturnNotFound_WhenQuestionnaireDoesNotExist()
    {
        // Arrange
        _mockService.Setup(x => x.GetByIdAsync("nonexistent")).ReturnsAsync((Questionnaire?)null);

        // Act
        var result = await _controller.GetById("nonexistent");

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }
}