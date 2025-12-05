using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using AI4NGExperimentsLambda.Controllers;
using AI4NGExperimentsLambda.Interfaces;
using AI4NGExperimentsLambda.Models;
using AI4NGExperimentManagement.Shared;

namespace AI4NGExperiments.Tests;

public class ValidationErrorTests
{
    private readonly Mock<IExperimentService> _mockExperimentService;
    private readonly Mock<ITaskService> _mockTaskService;
    private readonly Mock<IAuthenticationService> _mockAuthService;
    private readonly ExperimentsController _experimentsController;
    private readonly TasksController _tasksController;

    public ValidationErrorTests()
    {
        _mockExperimentService = new Mock<IExperimentService>();
        _mockTaskService = new Mock<ITaskService>();
        _mockAuthService = new Mock<IAuthenticationService>();

        _mockAuthService.Setup(x => x.IsResearcher()).Returns(true);
        _mockAuthService.Setup(x => x.GetUsernameFromRequest()).Returns("testuser");

        _experimentsController = new ExperimentsController(_mockExperimentService.Object, _mockAuthService.Object);
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
            .Setup(x => x.CreateExperimentAsync(It.IsAny<Experiment>(), It.IsAny<string>()))
            .ThrowsAsync(new ArgumentException("Missing questionnaires: MissingQ1, MissingQ2"));

        // Act
        var result = await _experimentsController.Create(experiment);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        AssertValidationError(badRequestResult.Value, "MissingQ1", "MissingQ2");
    }

    [Fact]
    public async Task CreateTask_WithMissingQuestionnaire_ReturnsBadRequestWithValidationError()
    {
        // Arrange
        var taskRequest = new CreateTaskRequest
        {
            Name = "Test Task",
            Type = "questionnaire",
            Configuration = new Dictionary<string, object>
            {
                ["questionnaireId"] = "NonExistentQuestionnaire"
            }
        };

        _mockTaskService
            .Setup(x => x.CreateTaskAsync(It.IsAny<CreateTaskRequest>(), It.IsAny<string>()))
            .ThrowsAsync(new ArgumentException("Missing questionnaires: NonExistentQuestionnaire"));

        // Act
        var result = await _tasksController.Create(taskRequest);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        AssertValidationError(badRequestResult.Value, "NonExistentQuestionnaire");
    }

    [Fact]
    public async Task CreateSession_WithMissingExperiment_ReturnsBadRequestWithValidationError()
    {
        // Arrange
        var sessionRequest = new CreateSessionRequest
        {
            SessionType = "DAILY",
            SessionName = "Test Session",
            Date = "2024-01-15"
        };

        _mockExperimentService
            .Setup(x => x.CreateSessionAsync(It.IsAny<string>(), It.IsAny<CreateSessionRequest>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Experiment 'non-existent' not found"));

        // Act
        var result = await _experimentsController.CreateSession("non-existent", sessionRequest);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);

        // Shared validation error checks
        AssertValidationError(badRequestResult.Value);

        // Plus the specific message assertion for this case
        var message = GetStringProperty(badRequestResult.Value, "message");
        Assert.NotNull(message);
        Assert.Contains("not found", message);
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
            .Setup(x => x.ValidateExperimentAsync(It.IsAny<Experiment>()))
            .ReturnsAsync(validationResult);

        // Act
        var result = await _experimentsController.ValidateExperiment(experiment);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = okResult.Value;

        var valid = GetProperty<bool>(value, "valid");
        Assert.False(valid);

        var missingQuestionnaires = GetProperty<string[]>(value, "missingQuestionnaires");
        Assert.NotNull(missingQuestionnaires);
        Assert.Contains("MissingQ1", missingQuestionnaires);
        Assert.Contains("MissingQ2", missingQuestionnaires);

        var message = GetStringProperty(value, "message");
        Assert.NotNull(message);
        Assert.Contains("Missing questionnaires", message);
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
