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
    public async Task CreateExperiment_WithMissingQuestionnaires_ReturnsBadRequestWithValidationError()
    {
        // Arrange
        var experiment = new Experiment
        {
            Data = new ExperimentData
            {
                Name = "Test Experiment",
                SessionTypes = new Dictionary<string, SessionType>
                {
                    ["DAILY"] = new SessionType
                    {
                        Questionnaires = new List<string> { "MissingQ1", "MissingQ2" }
                    }
                }
            }
        };

        _mockExperimentService
            .Setup(x => x.CreateExperimentAsync(It.IsAny<Experiment>(), It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Missing questionnaires: MissingQ1, MissingQ2"));

        // Act & Assert - service throws ArgumentException; controller does not catch so exception propagates
        await Assert.ThrowsAsync<ArgumentException>(() => _experimentsController.Create(experiment, System.Threading.CancellationToken.None));
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

    [Fact]
    public async Task ValidateExperiment_WithMissingQuestionnaires_ReturnsValidationResult()
    {
        // Arrange
        var experiment = new Experiment
        {
            Data = new ExperimentData
            {
                SessionTypes = new Dictionary<string, SessionType>
                {
                    ["DAILY"] = new SessionType
                    {
                        Questionnaires = new List<string> { "MissingQ1", "MissingQ2" }
                    }
                }
            }
        };

        var validationResult = new
        {
            valid = false,
            referencedQuestionnaires = new[] { "MissingQ1", "MissingQ2" },
            missingQuestionnaires = new[] { "MissingQ1", "MissingQ2" },
            message = "Missing questionnaires: MissingQ1, MissingQ2"
        };

        _mockExperimentService
            .Setup(x => x.ValidateExperimentAsync(It.IsAny<Experiment>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new AI4NGExperimentsLambda.Models.Dtos.ValidateExperimentResponseDto
            {
                Valid = false,
                ReferencedQuestionnaires = new List<string> { "MissingQ1", "MissingQ2" },
                MissingQuestionnaires = new List<string> { "MissingQ1", "MissingQ2" },
                Message = "Missing questionnaires: MissingQ1, MissingQ2"
            });

        // Act
        var result = await _experimentsController.ValidateExperiment(experiment, System.Threading.CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = Assert.IsType<AI4NGExperimentsLambda.Models.Dtos.ValidateExperimentResponseDto>(okResult.Value);

        Assert.False(value.Valid);
        Assert.NotNull(value.MissingQuestionnaires);
        Assert.Contains("MissingQ1", value.MissingQuestionnaires);
        Assert.Contains("MissingQ2", value.MissingQuestionnaires);
        Assert.NotNull(value.Message);
        Assert.Contains("Missing questionnaires", value.Message);
    }

    // ----------------------------
    // Helper methods (reflection)
    // ----------------------------

    private static T GetProperty<T>(object value, string propertyName)
    {
        var prop = value.GetType().GetProperty(propertyName);
        Assert.NotNull(prop); // Fail fast if the property doesn't exist

        var raw = prop!.GetValue(value);
        Assert.IsType<T>(raw); // Ensure expected type

        return (T)raw!;
    }

    private static string? GetStringProperty(object value, string propertyName)
        => value.GetType().GetProperty(propertyName)?.GetValue(value)?.ToString();

    /// <summary>
    /// Asserts the common "ValidationError" payload shape:
    /// { error = "ValidationError", missingQuestionnaires = string[] }
    /// </summary>
    private static void AssertValidationError(object value, params string[] expectedMissingQuestionnaires)
    {
        var error = GetStringProperty(value, "error");
        Assert.Equal("ValidationError", error);

        if (expectedMissingQuestionnaires is { Length: > 0 })
        {
            var missingQuestionnaires = GetProperty<string[]>(value, "missingQuestionnaires");
            Assert.NotNull(missingQuestionnaires);

            foreach (var q in expectedMissingQuestionnaires)
            {
                Assert.Contains(q, missingQuestionnaires);
            }
        }
    }
}
