using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using AI4NGQuestionnairesLambda.Controllers;
using AI4NGQuestionnairesLambda.Interfaces;
using AI4NG.ExperimentManagement.Contracts.Questionnaires;
using AI4NGExperimentManagement.Shared;
using AI4NGExperimentManagementTests.Shared;

namespace AI4NGQuestionnaires.Tests;

[Collection("QuestionnairesCollection")]
public class QuestionnaireCreateTest : ControllerTestBase<QuestionnairesController>
{
    [Fact]
    public async Task Create_ShouldReturnOk_WhenValidRequest()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<IQuestionnaireService>(
            (service, auth) => new QuestionnairesController(service, auth));

        var request = new CreateQuestionnaireRequest
        {
            Id = "test-id",
            Data = new QuestionnaireDataDto { Name = "Test Questionnaire" }
        };

        mockService.Setup(x => x.CreateAsync(request.Id, request.Data, TestDataBuilder.TestUsername))
                  .ReturnsAsync("created-id");

        // Act
        var result = await controller.Create(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = okResult.Value;
        Assert.NotNull(response);
    }
}