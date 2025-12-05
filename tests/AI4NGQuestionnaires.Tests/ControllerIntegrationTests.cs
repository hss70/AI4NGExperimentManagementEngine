using Xunit;
using Microsoft.AspNetCore.Mvc;
using Moq;
using AI4NGQuestionnairesLambda.Controllers;
using AI4NGQuestionnairesLambda.Interfaces;
using AI4NGQuestionnairesLambda.Models;
using AI4NGExperimentManagement.Shared;
using AI4NGExperimentManagementTests.Shared;

namespace AI4NGQuestionnaires.Tests;

public class ControllerIntegrationTests : ControllerTestBase<QuestionnairesController>
{
    [Fact]
    public async Task QuestionnairesController_GetAll_ShouldReturnOkWithQuestionnaires()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IQuestionnaireService>(
            (service, auth) => new QuestionnairesController(service, auth));

        var questionnaires = new List<Questionnaire> 
        { 
            new() { Id = "q1", Data = new QuestionnaireData { Name = "Test Questionnaire 1" } },
            new() { Id = "q2", Data = new QuestionnaireData { Name = "Test Questionnaire 2" } }
        };
        mockService.Setup(x => x.GetAllAsync()).ReturnsAsync(questionnaires);

        // Act
        var result = await controller.GetAll();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(questionnaires, okResult.Value);
    }

    [Fact]
    public async Task QuestionnairesController_GetById_ShouldReturnOkWithQuestionnaire()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IQuestionnaireService>(
            (service, auth) => new QuestionnairesController(service, auth));

        var questionnaire = new Questionnaire 
        { 
            Id = "test-id", 
            Data = new QuestionnaireData { Name = "Test Questionnaire" } 
        };
        mockService.Setup(x => x.GetByIdAsync("test-id")).ReturnsAsync(questionnaire);

        // Act
        var result = await controller.GetById("test-id");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(questionnaire, okResult.Value);
    }

    [Fact]
    public async Task QuestionnairesController_GetById_ShouldReturnNotFoundWhenQuestionnaireDoesNotExist()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IQuestionnaireService>(
            (service, auth) => new QuestionnairesController(service, auth));

        mockService.Setup(x => x.GetByIdAsync("nonexistent")).ReturnsAsync((Questionnaire?)null);

        // Act
        var result = await controller.GetById("nonexistent");

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Equal("Questionnaire not found", notFoundResult.Value);
    }

    [Fact]
    public async Task QuestionnairesController_Create_ShouldReturnOkWithId()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IQuestionnaireService>(
            (service, auth) => new QuestionnairesController(service, auth));

        var request = new CreateQuestionnaireRequest
        {
            Id = "test-id",
            Data = new QuestionnaireData { Name = "Test Questionnaire" }
        };
        mockService.Setup(x => x.CreateAsync(request, TestDataBuilder.TestUsername)).ReturnsAsync("test-id");

        // Act
        var result = await controller.Create(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = okResult.Value;
        Assert.NotNull(response);
        Assert.Contains("test-id", response.ToString());
    }

    [Fact]
    public async Task QuestionnairesController_Update_ShouldReturnOkWithMessage()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IQuestionnaireService>(
            (service, auth) => new QuestionnairesController(service, auth));

        var request = new CreateQuestionnaireRequest
        {
            Data = new QuestionnaireData { Name = "Updated Questionnaire" }
        };
        mockService.Setup(x => x.UpdateAsync("test-id", request.Data, TestDataBuilder.TestUsername)).Returns(Task.CompletedTask);

        // Act
        var result = await controller.Update("test-id", request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = okResult.Value;
        Assert.NotNull(response);
        Assert.Contains("updated successfully", response.ToString());
    }

    [Fact]
    public async Task QuestionnairesController_Update_ShouldReturnBadRequestWhenRequestIsNull()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IQuestionnaireService>(
            (service, auth) => new QuestionnairesController(service, auth));

        // Act
        var result = await controller.Update("test-id", null);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Invalid questionnaire update request", badRequestResult.Value.ToString());
    }

    [Fact]
    public async Task QuestionnairesController_Update_ShouldReturnBadRequestWhenIdIsEmpty()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IQuestionnaireService>(
            (service, auth) => new QuestionnairesController(service, auth));

        var request = new CreateQuestionnaireRequest
        {
            Data = new QuestionnaireData { Name = "Updated Questionnaire" }
        };

        // Act
        var result = await controller.Update("", request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Questionnaire ID cannot be empty", badRequestResult.Value.ToString());
    }

    [Fact]
    public async Task QuestionnairesController_Delete_ShouldReturnOkWithMessage()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IQuestionnaireService>(
            (service, auth) => new QuestionnairesController(service, auth));

        mockService.Setup(x => x.DeleteAsync("test-id", TestDataBuilder.TestUsername)).Returns(Task.CompletedTask);

        // Act
        var result = await controller.Delete("test-id");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = okResult.Value;
        Assert.NotNull(response);
        Assert.Contains("deleted successfully", response.ToString());
    }

    [Fact]
    public async Task QuestionnairesController_CreateBatch_ShouldReturnOkWhenAllSucceed()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IQuestionnaireService>(
            (service, auth) => new QuestionnairesController(service, auth));

        var requests = new List<CreateQuestionnaireRequest>
        {
            new() { Id = "q1", Data = new QuestionnaireData { Name = "Questionnaire 1" } },
            new() { Id = "q2", Data = new QuestionnaireData { Name = "Questionnaire 2" } }
        };

        var batchResult = new BatchResult(
            new BatchSummary(2, 2, 0),
            new List<BatchItemResult>
            {
                new BatchItemResult("q1", "success"),
                new BatchItemResult("q2", "success")
            }
        );

        mockService.Setup(x => x.CreateBatchAsync(requests, TestDataBuilder.TestUsername)).ReturnsAsync(batchResult);

        // Act
        var result = await controller.CreateBatch(requests);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(batchResult, okResult.Value);
    }

    [Fact]
    public async Task QuestionnairesController_CreateBatch_ShouldReturnBadRequestWhenAllFail()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IQuestionnaireService>(
            (service, auth) => new QuestionnairesController(service, auth));

        var requests = new List<CreateQuestionnaireRequest>
        {
            new() { Id = "q1", Data = new QuestionnaireData { Name = "Questionnaire 1" } }
        };

        var batchResult = new BatchResult(
            new BatchSummary(1, 0, 1),
            new List<BatchItemResult>
            {
                new BatchItemResult("q1", "failed", "Validation failed")
            }
        );

        mockService.Setup(x => x.CreateBatchAsync(requests, TestDataBuilder.TestUsername)).ReturnsAsync(batchResult);

        // Act
        var result = await controller.CreateBatch(requests);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(batchResult, badRequestResult.Value);
    }

    [Fact]
    public async Task QuestionnairesController_CreateBatch_ShouldReturnPartialSuccessWhenSomeFail()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IQuestionnaireService>(
            (service, auth) => new QuestionnairesController(service, auth));

        var requests = new List<CreateQuestionnaireRequest>
        {
            new() { Id = "q1", Data = new QuestionnaireData { Name = "Questionnaire 1" } },
            new() { Id = "q2", Data = new QuestionnaireData { Name = "Questionnaire 2" } }
        };

        var batchResult = new BatchResult(
            new BatchSummary(2, 1, 1),
            new List<BatchItemResult>
            {
                new BatchItemResult("q1", "success"),
                new BatchItemResult("q2", "failed", "Duplicate ID")
            }
        );

        mockService.Setup(x => x.CreateBatchAsync(requests, TestDataBuilder.TestUsername)).ReturnsAsync(batchResult);

        // Act
        var result = await controller.CreateBatch(requests);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(207, statusCodeResult.StatusCode); // Multi-Status
        Assert.Equal(batchResult, statusCodeResult.Value);
    }

    [Fact]
    public async Task QuestionnairesController_RequiresResearcherForCreateOperations()
    {
        // Arrange - Create controller with non-researcher user
        var (mockService, controller, _) = CreateControllerWithMocks<IQuestionnaireService>(
            (service, auth) => new QuestionnairesController(service, auth), isResearcher: false);

        var request = new CreateQuestionnaireRequest
        {
            Id = "test-id",
            Data = new QuestionnaireData { Name = "Test Questionnaire" }
        };

        // Act
        var result = await controller.Create(request);

        // Assert - Should return Forbidden
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objectResult.StatusCode);
        mockService.Verify(x => x.CreateAsync(It.IsAny<CreateQuestionnaireRequest>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task QuestionnairesController_RequiresResearcherForUpdateOperations()
    {
        // Arrange - Create controller with non-researcher user
        var (mockService, controller, _) = CreateControllerWithMocks<IQuestionnaireService>(
            (service, auth) => new QuestionnairesController(service, auth), isResearcher: false);

        var request = new CreateQuestionnaireRequest
        {
            Data = new QuestionnaireData { Name = "Updated Questionnaire" }
        };

        // Act
        var result = await controller.Update("test-id", request);

        // Assert - Should return Forbidden
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objectResult.StatusCode);
        mockService.Verify(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<QuestionnaireData>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task QuestionnairesController_RequiresResearcherForDeleteOperations()
    {
        // Arrange - Create controller with non-researcher user
        var (mockService, controller, _) = CreateControllerWithMocks<IQuestionnaireService>(
            (service, auth) => new QuestionnairesController(service, auth), isResearcher: false);

        // Act
        var result = await controller.Delete("test-id");

        // Assert - Should return Forbidden
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objectResult.StatusCode);
        mockService.Verify(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task QuestionnairesController_RequiresResearcherForBatchOperations()
    {
        // Arrange - Create controller with non-researcher user
        var (mockService, controller, _) = CreateControllerWithMocks<IQuestionnaireService>(
            (service, auth) => new QuestionnairesController(service, auth), isResearcher: false);

        var requests = new List<CreateQuestionnaireRequest>
        {
            new() { Id = "q1", Data = new QuestionnaireData { Name = "Questionnaire 1" } }
        };

        // Act
        var result = await controller.CreateBatch(requests);

        // Assert - Should return Forbidden
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objectResult.StatusCode);
        mockService.Verify(x => x.CreateBatchAsync(It.IsAny<List<CreateQuestionnaireRequest>>(), It.IsAny<string>()), Times.Never);
    }
}