using Xunit;
using Moq;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGQuestionnairesLambda.Services;
using AI4NG.ExperimentManagement.Contracts.Questionnaires;

namespace AI4NGQuestionnaires.Tests;

public class SoftDeleteTests
{
    private readonly Mock<IAmazonDynamoDB> _mockDynamoClient;
    private readonly QuestionnaireService _service;

    public SoftDeleteTests()
    {
        _mockDynamoClient = new Mock<IAmazonDynamoDB>();
        Environment.SetEnvironmentVariable("QUESTIONNAIRES_TABLE", "test-table");
        _service = new QuestionnaireService(_mockDynamoClient.Object);
    }

    [Fact]
    public async Task CreateAsync_ShouldAllowReuseOfSoftDeletedId()
    {
        // Arrange
        var request = new CreateQuestionnaireRequest
        {
            Id = "reused-id",
            Data = new QuestionnaireDataDto { Name = "New Questionnaire" }
        };

        // Mock existing soft-deleted item
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse
            {
                IsItemSet = true,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue("QUESTIONNAIRE#reused-id"),
                    ["syncMetadata"] = new AttributeValue
                    {
                        M = new Dictionary<string, AttributeValue>
                        {
                            ["isDeleted"] = new AttributeValue { BOOL = true }
                        }
                    }
                }
            });

        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ReturnsAsync(new PutItemResponse());

        // Act
        var result = await _service.CreateAsync(request.Id, request.Data, "testuser");

        // Assert
        Assert.Equal("reused-id", result);
        _mockDynamoClient.Verify(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowException_WhenActiveItemExists()
    {
        // Arrange
        var request = new CreateQuestionnaireRequest
        {
            Id = "existing-id",
            Data = new QuestionnaireDataDto { Name = "Test" }
        };

        // Mock existing active item
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse
            {
                IsItemSet = true,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue("QUESTIONNAIRE#existing-id"),
                    ["syncMetadata"] = new AttributeValue
                    {
                        M = new Dictionary<string, AttributeValue>
                        {
                            ["isDeleted"] = new AttributeValue { BOOL = false }
                        }
                    }
                }
            });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateAsync(request.Id, request.Data, "testuser"));

        Assert.Contains("already exists", exception.Message);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenItemIsSoftDeleted()
    {
        // Arrange
        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse
            {
                IsItemSet = true,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue("QUESTIONNAIRE#deleted-id"),
                    ["data"] = new AttributeValue { M = new Dictionary<string, AttributeValue> { ["name"] = new AttributeValue("Test") } },
                    ["syncMetadata"] = new AttributeValue
                    {
                        M = new Dictionary<string, AttributeValue>
                        {
                            ["isDeleted"] = new AttributeValue { BOOL = true }
                        }
                    }
                }
            });

        // Act
        var result = await _service.GetByIdAsync("deleted-id");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_ShouldSetSoftDeleteFlags()
    {
        // Arrange
        UpdateItemRequest? capturedRequest = null;
        _mockDynamoClient.Setup(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .Callback<UpdateItemRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new UpdateItemResponse());

        // Act
        await _service.DeleteAsync("test-id", "testuser");

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Contains(":deletedBy", capturedRequest.ExpressionAttributeValues.Keys);
        Assert.Contains(":timestamp", capturedRequest.ExpressionAttributeValues.Keys);
        Assert.Contains(":deleted", capturedRequest.ExpressionAttributeValues.Keys);
        Assert.True(capturedRequest.ExpressionAttributeValues[":deleted"].BOOL);
        Assert.Equal("testuser", capturedRequest.ExpressionAttributeValues[":deletedBy"].S);
    }
}