using Xunit;
using Moq;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGQuestionnairesLambda.Services;
using AI4NG.ExperimentManagement.Contracts.Questionnaires;
using AI4NGExperimentManagementTests.Shared;

namespace AI4NGQuestionnaires.Tests;

public class ReferentialIntegrityTests
{
    private readonly Mock<IAmazonDynamoDB> _mockDynamoClient;
    private readonly QuestionnaireService _service;

    public ReferentialIntegrityTests()
    {
        _mockDynamoClient = new Mock<IAmazonDynamoDB>();
        Environment.SetEnvironmentVariable("QUESTIONNAIRES_TABLE", "test-table");
        _service = new QuestionnaireService(_mockDynamoClient.Object);
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowException_WhenQuestionnaireAlreadyExists()
    {
        // Arrange
        var request = new CreateQuestionnaireRequest
        {
            Id = "existing-questionnaire",
            Data = TestDataBuilder.CreateValidQuestionnaireData()
        };

        // Mock questionnaire already exists (simulate Put conditional failure)
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ThrowsAsync(new ConditionalCheckFailedException("Conditional check failed"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateAsync(request.Id, request.Data, "testuser"));

        Assert.Contains("already exists", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_ShouldSucceed_WhenQuestionnaireDoesNotExist()
    {
        // Arrange
        var request = new CreateQuestionnaireRequest
        {
            Id = "new-questionnaire",
            Data = TestDataBuilder.CreateValidQuestionnaireData()
        };

        // Mock questionnaire does not exist
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ReturnsAsync(new PutItemResponse());

        // Act
        var result = await _service.CreateAsync(request.Id, request.Data, "testuser");

        // Assert
        Assert.NotNull(result);
        _mockDynamoClient.Verify(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_ShouldSucceed_WhenQuestionnaireExists()
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .ReturnsAsync(new UpdateItemResponse());

        // Act
        await _service.DeleteAsync("existing-questionnaire", "testuser");

        // Assert
        _mockDynamoClient.Verify(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ShouldNotValidateExistence()
    {
        // Arrange
        var data = TestDataBuilder.CreateValidQuestionnaireData();
        _mockDynamoClient.Setup(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .ReturnsAsync(new UpdateItemResponse());

        // Act - Should not throw even if questionnaire doesn't exist
        await _service.UpdateAsync("non-existent", data, "testuser");

        // Assert
        _mockDynamoClient.Verify(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default), Times.Once);
    }
}