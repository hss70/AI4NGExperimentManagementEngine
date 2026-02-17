using Xunit;
using Moq;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGQuestionnairesLambda.Services;
using AI4NG.ExperimentManagement.Contracts.Questionnaires;

namespace AI4NGQuestionnaires.Tests;

public class ValidationTests
{
    private readonly Mock<IAmazonDynamoDB> _mockDynamoClient;
    private readonly QuestionnaireService _service;

    public ValidationTests()
    {
        _mockDynamoClient = new Mock<IAmazonDynamoDB>();
        Environment.SetEnvironmentVariable("QUESTIONNAIRES_TABLE", "test-table");
        _service = new QuestionnaireService(_mockDynamoClient.Object);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task CreateAsync_ShouldThrowException_WhenIdIsInvalid(string? invalidId)
    {
        // Arrange
        var request = new CreateQuestionnaireRequest
        {
            Id = invalidId!,
            Data = new QuestionnaireDataDto { Name = "Test" }
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(request.Id, request.Data, "testuser"));
    }

    [Fact]
    public async Task CreateAsync_ShouldSucceed_WhenValidData()
    {
        // Arrange
        var request = new CreateQuestionnaireRequest
        {
            Id = "valid-questionnaire",
            Data = new QuestionnaireDataDto
            {
                Name = "Valid Questionnaire",
                Description = "Valid description",
                Questions = new List<QuestionDto>
                {
                    new() { Id = "q1", Text = "Question 1", Type = "text", Required = true }
                }
            }
        };

        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse { Item = null });
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ReturnsAsync(new PutItemResponse());

        // Act
        var result = await _service.CreateAsync(request.Id, request.Data, "testuser");

        // Assert
        Assert.NotNull(result);
        _mockDynamoClient.Verify(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task GetByIdAsync_ShouldHandleInvalidQuestionnaireIds(string? invalidId)
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse { Item = null });

        // Act
        var result = await _service.GetByIdAsync(invalidId!);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateAsync_ShouldValidateQuestionStructure()
    {
        // Arrange
        var request = new CreateQuestionnaireRequest
        {
            Id = "test-questionnaire",
            Data = new QuestionnaireDataDto
            {
                Name = "Test Questionnaire",
                Questions = new List<QuestionDto>
                {
                    new() { Id = "q1", Text = "", Type = "text", Required = true } // Empty text
                }
            }
        };

        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse { Item = null });

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(request.Id, request.Data, "testuser"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task UpdateAsync_ShouldHandleInvalidUsernames(string? username)
    {
        // Arrange
        var data = new QuestionnaireDataDto { Name = "Updated" };
        _mockDynamoClient.Setup(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .ReturnsAsync(new UpdateItemResponse());

        // Act
        await _service.UpdateAsync("test-id", data, username!);

        // Assert
        _mockDynamoClient.Verify(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_ShouldHandleEmptyQuestionsList()
    {
        // Arrange
        var request = new CreateQuestionnaireRequest
        {
            Id = "empty-questions",
            Data = new QuestionnaireDataDto
            {
                Name = "Empty Questions",
                Questions = new List<QuestionDto>() // Empty list
            }
        };

        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse { Item = null });
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ReturnsAsync(new PutItemResponse());

        // Act
        var result = await _service.CreateAsync(request.Id, request.Data, "testuser");

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task CreateAsync_ShouldHandleNullQuestionsList()
    {
        // Arrange
        var request = new CreateQuestionnaireRequest
        {
            Id = "null-questions",
            Data = new QuestionnaireDataDto
            {
                Name = "Null Questions",
                Questions = null! // Null list
            }
        };

        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse { Item = null });
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ReturnsAsync(new PutItemResponse());

        // Act
        var result = await _service.CreateAsync(request.Id, request.Data, "testuser");

        // Assert
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData("invalid-type")]
    [InlineData("")]
    [InlineData(null)]
    public async Task CreateAsync_ShouldHandleInvalidQuestionTypes(string? invalidType)
    {
        // Arrange
        var request = new CreateQuestionnaireRequest
        {
            Id = "invalid-type-test",
            Data = new QuestionnaireDataDto
            {
                Name = "Invalid Type Test",
                Questions = new List<QuestionDto>
                {
                    new() { Id = "q1", Text = "Question 1", Type = invalidType!, Required = true }
                }
            }
        };

        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse { Item = null });

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(request.Id, request.Data, "testuser"));
    }
}