using Xunit;
using Moq;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGQuestionnairesLambda.Services;
using AI4NGQuestionnairesLambda.Models;

namespace AI4NGQuestionnaires.Tests;

public class BusinessRuleTests
{
    private readonly Mock<IAmazonDynamoDB> _mockDynamoClient;
    private readonly QuestionnaireService _service;

    public BusinessRuleTests()
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

        // Mock existing questionnaire
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse 
            { 
                IsItemSet = true,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue("QUESTIONNAIRE#existing-questionnaire")
                }
            });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateAsync(request, "testuser"));
        
        Assert.Contains("already exists", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task CreateAsync_ShouldThrowException_WhenNameIsInvalid(string invalidName)
    {
        // Arrange
        var request = new CreateQuestionnaireRequest
        {
            Id = "test-questionnaire",
            Data = new QuestionnaireData { Name = invalidName }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(request, "testuser"));
        
        Assert.Contains("name", exception.Message.ToLower());
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowException_WhenQuestionIdsNotUnique()
    {
        // Arrange
        var request = new CreateQuestionnaireRequest
        {
            Id = "test-questionnaire",
            Data = new QuestionnaireData 
            { 
                Name = "Test Questionnaire",
                Questions = new List<Question>
                {
                    new() { Id = "q1", Text = "Question 1", Type = "text", Required = true },
                    new() { Id = "q1", Text = "Question 2", Type = "text", Required = true } // Duplicate ID
                }
            }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(request, "testuser"));
        
        Assert.Contains("unique", exception.Message.ToLower());
        Assert.Contains("question", exception.Message.ToLower());
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowException_WhenRequiredFieldsMissing()
    {
        // Arrange
        var request = new CreateQuestionnaireRequest
        {
            Id = "test-questionnaire",
            Data = new QuestionnaireData 
            { 
                Name = "Test Questionnaire",
                Questions = new List<Question>
                {
                    new() { Id = "q1", Text = "", Type = "text", Required = true } // Missing text
                }
            }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(request, "testuser"));
        
        Assert.Contains("text", exception.Message.ToLower());
    }

    [Theory]
    [InlineData("invalid-type")]
    [InlineData("")]
    [InlineData(null)]
    public async Task CreateAsync_ShouldThrowException_WhenQuestionTypeInvalid(string invalidType)
    {
        // Arrange
        var request = new CreateQuestionnaireRequest
        {
            Id = "test-questionnaire",
            Data = new QuestionnaireData 
            { 
                Name = "Test Questionnaire",
                Questions = new List<Question>
                {
                    new() { Id = "q1", Text = "Question 1", Type = invalidType, Required = true }
                }
            }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(request, "testuser"));
        
        Assert.Contains("type", exception.Message.ToLower());
    }

    [Fact]
    public async Task CreateAsync_ShouldRequireOptions_WhenQuestionTypeIsSelect()
    {
        // Arrange
        var request = new CreateQuestionnaireRequest
        {
            Id = "test-questionnaire",
            Data = new QuestionnaireData 
            { 
                Name = "Test Questionnaire",
                Questions = new List<Question>
                {
                    new() { Id = "q1", Text = "Select question", Type = "select", Required = true } // Missing options
                }
            }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(request, "testuser"));
        
        Assert.Contains("options", exception.Message.ToLower());
        Assert.Contains("select", exception.Message.ToLower());
    }
}