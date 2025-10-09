using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Amazon.DynamoDBv2;
using AI4NGQuestionnairesLambda;
using System.Text;
using System.Text.Json;

namespace AI4NGQuestionnaires.Tests;

public class IntegrationTests : IClassFixture<WebApplicationFactory<Startup>>
{
    private readonly WebApplicationFactory<Startup> _factory;
    private readonly HttpClient _client;

    public IntegrationTests(WebApplicationFactory<Startup> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace DynamoDB with in-memory mock for integration tests
                services.AddSingleton<IAmazonDynamoDB>(provider => 
                {
                    var config = new Amazon.DynamoDBv2.AmazonDynamoDBConfig 
                    { 
                        ServiceURL = "http://localhost:8000" 
                    };
                    return new Amazon.DynamoDBv2.AmazonDynamoDBClient(config);
                });
            });
        });
        
        _client = _factory.CreateClient();
        
        // Set local testing environment
        Environment.SetEnvironmentVariable("AWS_ENDPOINT_URL", "http://localhost:8000");
        Environment.SetEnvironmentVariable("QUESTIONNAIRES_TABLE", "questionnaires-test");
    }

    [Fact]
    public async Task GetQuestionnaires_ShouldReturn200()
    {
        // Act
        var response = await _client.GetAsync("/api/questionnaires");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/json; charset=utf-8", response.Content.Headers.ContentType?.ToString());
    }

    [Fact]
    public async Task CreateQuestionnaire_ShouldReturn200_WithValidData()
    {
        // Arrange
        var questionnaire = new
        {
            id = "integration-test",
            data = new
            {
                name = "Integration Test Questionnaire",
                description = "Test questionnaire for integration testing"
            }
        };

        var json = JsonSerializer.Serialize(questionnaire);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/questionnaires", content);

        // Assert
        response.EnsureSuccessStatusCode();
        var responseContent = await response.Content.ReadAsStringAsync();
        Assert.Contains("integration-test", responseContent);
    }
}