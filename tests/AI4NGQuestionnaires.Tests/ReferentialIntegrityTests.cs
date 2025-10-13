using Xunit;
using Moq;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGQuestionnairesLambda.Services;
using AI4NGQuestionnairesLambda.Models;

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
            Data = new QuestionnaireData { Name = "Test" }
        };

        // Mock questionnaire already exists
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse 
            { 
                Item = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue("QUESTIONNAIRE#existing-questionnaire")
                }
            });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DuplicateItemException>(
            () => _service.CreateAsync(request, "testuser"));
        
        Assert.Contains("already exists", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_ShouldSucceed_WhenQuestionnaireDoesNotExist()
    {
        // Arrange
        var request = new CreateQuestionnaireRequest
        {
            Id = "new-questionnaire",
            Data = new QuestionnaireData { Name = "New Questionnaire" }
        };

        // Mock questionnaire does not exist
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse { Item = null });

        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ReturnsAsync(new PutItemResponse());

        // Act
        var result = await _service.CreateAsync(request, "testuser");

        // Assert
        Assert.NotNull(result);
        _mockDynamoClient.Verify(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default), Times.Once);
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
        var data = new QuestionnaireData { Name = "Updated" };
        _mockDynamoClient.Setup(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .ReturnsAsync(new UpdateItemResponse());

        // Act - Should not throw even if questionnaire doesn't exist
        await _service.UpdateAsync("non-existent", data, "testuser");

        // Assert
        _mockDynamoClient.Verify(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default), Times.Once);
    }
}