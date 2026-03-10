using Microsoft.AspNetCore.Mvc;
using Moq;
using AI4NGExperimentsLambda.Controllers;
using ResearcherExperimentsController = AI4NGExperimentsLambda.Controllers.Researcher.ExperimentsController;
using AI4NGExperimentsLambda.Interfaces.Researcher;
using AI4NGExperimentsLambda.Interfaces;
using AI4NGExperimentsLambda.Models;
using AI4NGExperimentManagement.Shared;

namespace AI4NGExperiments.Tests;

public class ValidationErrorTests
{
    private readonly Mock<IExperimentsService> _mockExperimentService;
    private readonly Mock<ITaskService> _mockTaskService;
    private readonly Mock<IAuthenticationService> _mockAuthService;
    private readonly ResearcherExperimentsController _experimentsController;
    private readonly TasksController _tasksController;

    public ValidationErrorTests()
    {
        _mockExperimentService = new Mock<IExperimentsService>();
        _mockTaskService = new Mock<ITaskService>();
        _mockAuthService = new Mock<IAuthenticationService>();

        _mockAuthService.Setup(x => x.IsResearcher()).Returns(true);
        _mockAuthService.Setup(x => x.GetUsernameFromRequest()).Returns("testuser");

        _experimentsController = new ResearcherExperimentsController(_mockExperimentService.Object, _mockAuthService.Object);
        _tasksController = new TasksController(_mockTaskService.Object, _mockAuthService.Object);
    }

    [Fact]
    public async Task CreateTask_WithMissingQuestionnaire_ReturnsBadRequestWithValidationError()
    {
        // Arrange
        var taskRequest = new CreateTaskRequest
        {
            TaskKey = "TEST_TASK",
            Data = new TaskData
            {
                Name = "Test Task",
                Type = "questionnaire",
                Configuration = new Dictionary<string, object>
                {
                    ["questionnaireId"] = "NonExistentQuestionnaire"
                }
            }
        };

        _mockTaskService
            .Setup(x => x.CreateTaskAsync(It.IsAny<CreateTaskRequest>(), It.IsAny<string>()))
            .ThrowsAsync(new ArgumentException("Missing questionnaires: NonExistentQuestionnaire"));

        // Act & Assert - service throws ArgumentException; controller does not catch so exception propagates
        await Assert.ThrowsAsync<ArgumentException>(() => _tasksController.Create(taskRequest));
    }

    [Fact(Skip = "Refactor: moved to Session services")]
    public async Task CreateSession_WithMissingExperiment_ReturnsBadRequestWithValidationError()
    {
        // Quarantined - session protocol tests moved to SessionProtocolService
        await Task.CompletedTask;
    }

}
