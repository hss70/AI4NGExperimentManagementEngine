using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using AI4NGQuestionnairesLambda.Controllers;
using AI4NGQuestionnairesLambda.Interfaces;
using AI4NG.ExperimentManagement.Contracts.Questionnaires;
using AI4NGExperimentManagement.Shared;
using AI4NGExperimentManagementTests.Shared;
using System.Net;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http;

namespace AI4NGQuestionnaires.Tests;


[Collection("QuestionnairesCollection")]
public class QuestionnairesControllerTests : ControllerTestBase<QuestionnairesController>, IDisposable
{
    private readonly string? _originalEndpointUrl;
    private readonly HttpClient _client;

    public QuestionnairesControllerTests()
    {
        _originalEndpointUrl = Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL");

        var webHostBuilder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();

                // Replace real JWT auth with a simple test auth that trusts JWT payload (no signature validation)
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });

                services.AddAuthorization(options =>
                {
                    options.AddPolicy("Researcher", policy => policy.RequireClaim("cognito:groups", "Researcher"));
                    options.AddPolicy("Participant", policy => policy.RequireClaim("cognito:groups", "Participant"));
                });
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();

                app.UseEndpoints(endpoints =>
                {
                    // Minimal endpoints used in tests to validate auth behavior
                    endpoints.MapGet("/api/researcher-endpoint", async context =>
                    {
                        context.Response.StatusCode = 200;
                        await Task.CompletedTask;
                    }).RequireAuthorization("Researcher");

                    endpoints.MapGet("/api/participant-endpoint", async context =>
                    {
                        context.Response.StatusCode = 200;
                        await Task.CompletedTask;
                    }).RequireAuthorization("Participant");
                });
            });

        var testServer = new TestServer(webHostBuilder);
        _client = testServer.CreateClient();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("AWS_ENDPOINT_URL", _originalEndpointUrl);
    }

    private (Mock<IQuestionnaireService> mockService, QuestionnairesController controller, Mock<AI4NGExperimentManagement.Shared.IAuthenticationService> authMock) CreateController(bool isLocal = true, bool isResearcher = true)
        => CreateControllerWithMocks<IQuestionnaireService>((svc, auth) => new QuestionnairesController(svc, auth), isLocal, isResearcher);

    [Fact]
    public async Task GetAll_ShouldReturnOk_WithQuestionnaires()
    {
        // Arrange
        var (mockService, controller, auth) = CreateController();
        var questionnaires = new List<QuestionnaireDto>
        {
            new() { Id = "test-1", Data = new QuestionnaireDataDto { Name = "Test 1" } }
        };
        mockService.Setup(x => x.GetAllAsync()).ReturnsAsync(questionnaires);

        // Act
        var result = await controller.GetAll();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(questionnaires, okResult.Value);
    }

    [Fact]
    public async Task Create_ShouldReturnOk_WhenValidRequest()
    {
        // Arrange
        var (mockService, controller, auth) = CreateController();
        var request = new CreateQuestionnaireRequest
        {
            Id = "test-id",
            Data = new QuestionnaireDataDto { Name = "Test" }
        };
        mockService.Setup(x => x.CreateAsync(request.Id, request.Data, TestDataBuilder.TestUsername)).ReturnsAsync(TestDataBuilder.TestUserId);
        controller.HttpContext.Request.Path = TestDataBuilder.Paths.ResearcherQuestionnaires;

        // Act
        var result = await controller.Create(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsAssignableFrom<object>(okResult.Value);
        Assert.NotNull(response);
    }

    [Theory]
    [InlineData(true, TestDataBuilder.TestUserId, true)]
    [InlineData(false, TestDataBuilder.NonExistentId, false)]
    public async Task GetById_ShouldReturnExpectedResult(bool exists, string id, bool expectOk)
    {
        // Arrange
        var (mockService, controller, auth) = CreateController();
        var questionnaire = new QuestionnaireDto { Id = TestDataBuilder.TestUserId, Data = new QuestionnaireDataDto { Name = "Test" } };
        if (exists)
            mockService.Setup(x => x.GetByIdAsync(id)).ReturnsAsync(questionnaire);
        else
            mockService.Setup(x => x.GetByIdAsync(id)).ReturnsAsync((QuestionnaireDto?)null);

        // Act
        var result = await controller.GetById(id);

        // Assert
        if (expectOk)
        {
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(questionnaire, okResult.Value);
        }
        else
        {
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
            Assert.Equal("Questionnaire not found", notFoundResult.Value);
        }
    }

    [Fact]
    public async Task Update_ShouldReturnOk_WhenValid()
    {
        // Arrange
        using var _ = TestEnvironmentHelper.SetLocalTestingMode();

        var (mockService, controller, auth) = CreateController();
        QuestionnaireDto questionnaireDto = TestDataBuilder.CreateValidQuestionnaire();
        mockService.Setup(x => x.UpdateAsync(questionnaireDto.Id, questionnaireDto.Data, TestDataBuilder.TestUsername)).Returns(Task.CompletedTask);
        controller.HttpContext.Request.Path = TestDataBuilder.Paths.ResearcherQuestionnaires;

        var request = new UpdateQuestionnaireRequest {Data = questionnaireDto.Data};
        // Act
        var result = await controller.Update(questionnaireDto.Id, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = okResult.Value as dynamic;
        Assert.NotNull(response);
    }

    [Fact]
    public async Task Delete_ShouldReturnOk_WhenValid()
    {
        // Arrange
        using var _ = TestEnvironmentHelper.SetLocalTestingMode();

        var (mockService, controller, auth) = CreateController();
        mockService.Setup(x => x.DeleteAsync(TestDataBuilder.TestUserId, TestDataBuilder.TestUsername)).Returns(Task.CompletedTask);
        controller.HttpContext.Request.Path = TestDataBuilder.Paths.ResearcherQuestionnaires;

        // Act
        var result = await controller.Delete(TestDataBuilder.TestUserId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = okResult.Value as dynamic;
        Assert.NotNull(response);
    }

    [Fact]
    public async Task Create_ShouldReturnForbidden_WhenNotResearcher()
    {
        // Arrange
        var (mockService, controller, _) = CreateController(isResearcher: false);
        var request = new CreateQuestionnaireRequest { Id = "test", Data = new QuestionnaireDataDto { Name = "Test" } };
        controller.HttpContext.Request.Path = TestDataBuilder.Paths.ParticipantQuestionnaires;

        // Act
        var result = await controller.Create(request);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objectResult.StatusCode);
        var messageProperty = objectResult.Value?.GetType().GetProperty("message");
        var message = messageProperty?.GetValue(objectResult.Value)?.ToString();

        Assert.Equal("Participants cannot perform this action", message);
    }

    [Fact]
    public async Task GetUsernameFromJwt_ShouldReturnTestUser_InLocalMode()
    {
        // Arrange
        var (mockService, controller, auth) = CreateController();
        var request = new CreateQuestionnaireRequest { Id = "test", Data = new QuestionnaireDataDto { Name = "Test" } };
        mockService.Setup(x => x.CreateAsync(request.Id, request.Data, TestDataBuilder.TestUsername)).ReturnsAsync("test");
        controller.HttpContext.Request.Path = TestDataBuilder.Paths.ResearcherQuestionnaires;

        // Act
        var result = await controller.Create(request);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        mockService.Verify(x => x.CreateAsync(request.Id, request.Data, TestDataBuilder.TestUsername), Times.Once);
    }

    [Fact]
    public async Task Create_ShouldReturnUnauthorized_WhenNoAuthInNonLocalMode()
    {
        // Arrange
        var (mockService, controller, auth) = CreateController(isLocal: false);
        auth.Setup(x => x.GetUsernameFromRequest()).Throws(new UnauthorizedAccessException("Authorization header is required"));
        controller.ControllerContext.HttpContext.Request.Headers.Clear();
        var request = new CreateQuestionnaireRequest { Id = "test", Data = new QuestionnaireDataDto { Name = "Test" } };

        // Act
        var result = await controller.Create(request);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Update_ShouldReturnUnauthorized_WhenNoAuthInNonLocalMode()
    {
        // Arrange
        var (mockService, controller, auth) = CreateController(isLocal: false);
        auth.Setup(x => x.GetUsernameFromRequest()).Throws(new UnauthorizedAccessException("Authorization header is required"));
        controller.ControllerContext.HttpContext.Request.Headers.Clear();
        var request = new UpdateQuestionnaireRequest { Data = new QuestionnaireDataDto { Name = "Test" } };

        // Act
        var result = await controller.Update("test-id", request);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Create_ShouldReturnUnauthorized_WhenNoAuthHeader()
    {
        // Arrange
        var (mockService, controller, auth) = CreateController(isLocal: false);
        auth.Setup(x => x.GetUsernameFromRequest()).Throws(new UnauthorizedAccessException("Authorization header is required"));
        controller.ControllerContext.HttpContext.Request.Headers.Clear();
        var request = new CreateQuestionnaireRequest { Id = "test", Data = new QuestionnaireDataDto { Name = "Test" } };

        // Act
        var result = await controller.Create(request);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Create_ShouldReturnUnauthorized_WhenInvalidToken()
    {
        // Arrange
        var (mockService, controller, auth) = CreateController(isLocal: false);
        auth.Setup(x => x.GetUsernameFromRequest()).Throws(new UnauthorizedAccessException("Invalid token format"));
        controller.ControllerContext.HttpContext.Request.Headers.Authorization = "Bearer invalid-token";
        var request = new CreateQuestionnaireRequest { Id = "test", Data = new QuestionnaireDataDto { Name = "Test" } };

        // Act
        var result = await controller.Create(request);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task ResearcherAccess_Allowed_WhenInResearcherGroup()
    {
        // Arrange
        var token = JwtTokenGenerator.GenerateToken("Researcher");
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/researcher-endpoint");

        // Assert
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task ResearcherAccess_Denied_WhenNotInResearcherGroup()
    {
        // Arrange
        var token = JwtTokenGenerator.GenerateToken("Participant");
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/researcher-endpoint");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ParticipantAccess_Denied_WhenNotInParticipantGroup()
    {
        // Arrange
        var token = JwtTokenGenerator.GenerateToken("Researcher"); // Not a Participant
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/participant-endpoint");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ParticipantAccess_Allowed_WhenInParticipantGroup()
    {
        // Arrange
        var token = JwtTokenGenerator.GenerateToken("Participant"); // Not a Participant
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/participant-endpoint");

        // Assert
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task NoToken_ReturnsUnauthorized()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = null; // No token

        // Act
        var response = await _client.GetAsync("/api/researcher-endpoint");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

public static class JwtTokenGenerator
{
    public static string GenerateToken(string group)
    {
        var claims = new[]
        {
            new System.Security.Claims.Claim("cognito:groups", group),
            new System.Security.Claims.Claim("cognito:username", "testuser")
        };

        // Use a key that is at least 256 bits (32 bytes) for HS256.
        // This is test code — use a sufficiently long test key here.
        var testKey = "YourVeryLongTestSecretKey_MustBeAtLeast32Bytes!";
        var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(testKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "https://your-issuer.com",
            audience: "your-audience",
            claims: claims,
            expires: DateTime.Now.AddMinutes(30),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

// Simple auth handler for tests: reads the bearer token as a JWT (without signature validation)
// and sets the claims principal accordingly.
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var header = Request.Headers["Authorization"].ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.Fail("No bearer token"));
        }

        var token = header.Substring("Bearer ".Length).Trim();
        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            var identity = new System.Security.Claims.ClaimsIdentity(jwt.Claims, Scheme.Name);
            var principal = new System.Security.Claims.ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
        catch
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid token"));
        }
    }
}
