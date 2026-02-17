using Xunit;
using Moq;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AI4NGQuestionnairesLambda.Services;
using AI4NGQuestionnaires.Tests;
using AI4NG.ExperimentManagement.Contracts.Questionnaires;

namespace AI4NGQuestionnaires.Tests;

public class DbSchemaComplianceTests
{
    private readonly Mock<IAmazonDynamoDB> _mockDynamoClient;
    private readonly QuestionnaireService _service;

    public DbSchemaComplianceTests()
    {
        _mockDynamoClient = new Mock<IAmazonDynamoDB>();
        Environment.SetEnvironmentVariable("QUESTIONNAIRES_TABLE", "test-table");

        _mockDynamoClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse { Item = null });
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ReturnsAsync(new PutItemResponse());

        _service = new QuestionnaireService(_mockDynamoClient.Object);
    }

    [Fact]
    public async Task CreateAsync_ShouldIncludeAllRequiredDbFields()
    {
        // Arrange
        var request = new CreateQuestionnaireRequest
        {
            Id = "PQ",
            Data = new QuestionnaireDataDto
            {
                Name = "Presence Questionnaire",
                Description = "Measures presence",
                EstimatedTime = 120,
                Version = "1.0",
                Questions = new List<QuestionDto>
                {
                    new() { Id = "1", Text = "Test question", Type = "scale", Required = true }
                }
            }
        };

        PutItemRequest? capturedRequest = null;
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .Callback<PutItemRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new PutItemResponse());

        // Act
        await _service.CreateAsync(request.Id, request.Data, "testuser");

        // Assert - Validate all DB Design required fields
        var item = capturedRequest!.Item;

        // Key structure
        Assert.Equal("QUESTIONNAIRE#PQ", item["PK"].S);
        Assert.Equal("CONFIG", item["SK"].S);

        // GSI3 for mobile sync
        Assert.Equal("QUESTIONNAIRE", item["GSI3PK"].S);
        Assert.True(item.ContainsKey("GSI3SK"));

        // Required attributes per DB Design
        Assert.Equal("Questionnaire", item["type"].S);
        Assert.True(item.ContainsKey("data"));
        Assert.True(item.ContainsKey("createdBy"));
        Assert.True(item.ContainsKey("updatedBy"));
        Assert.True(item.ContainsKey("createdAt"));
        Assert.True(item.ContainsKey("updatedAt"));

        // Sync metadata for mobile sync
        Assert.True(item.ContainsKey("syncMetadata"));
        var syncMetadata = item["syncMetadata"].M;
        Assert.True(syncMetadata.ContainsKey("version"));
        Assert.True(syncMetadata.ContainsKey("lastModified"));
        Assert.True(syncMetadata.ContainsKey("isDeleted"));
        Assert.False(syncMetadata["isDeleted"].BOOL);
    }

    [Fact]
    public async Task CreateAsync_ShouldValidateQuestionnaireDataStructure()
    {
        // Arrange
        var request = new CreateQuestionnaireRequest
        {
            Id = "PQ",
            Data = new QuestionnaireDataDto
            {
                Name = "Presence Questionnaire",
                Description = "Measures presence",
                EstimatedTime = 120,
                Version = "1.0",
                Questions = new List<QuestionDto>
                {
                    new()
                    {
                        Id = "1",
                        Text = "Time seemed to go by",
                        Type = "scale",
                        Required = true,
                        Options = new List<string> { "Quickly", "Slowly" }
                    }
                }
            }
        };

        PutItemRequest? capturedRequest = null;
        _mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .Callback<PutItemRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new PutItemResponse());

        // Act
        await _service.CreateAsync(request.Id, request.Data, "testuser");

        // Assert - Validate data structure matches DB Design
        var dataField = capturedRequest!.Item["data"].M;

        // Required data fields per DB Design
        Assert.Equal("Presence Questionnaire", dataField["name"].S);
        Assert.Equal("Measures presence", dataField["description"].S);
        Assert.Equal("120", dataField["estimatedTime"].N);
        Assert.Equal("1.0", dataField["version"].S);

        // Questions array structure
        Assert.True(dataField.ContainsKey("questions"));
        var questions = dataField["questions"].L;
        Assert.Single(questions);

        var question = questions[0].M;
        Assert.Equal("1", question["id"].S);
        Assert.Equal("Time seemed to go by", question["text"].S);
        Assert.Equal("scale", question["type"].S);
        Assert.True(question["required"].BOOL);
        Assert.True(question.ContainsKey("options"));
    }
}