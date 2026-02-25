using AI4NGExperimentManagement.Shared;
using AI4NGResponsesLambda.Controllers;
using AI4NGResponsesLambda.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace AI4NGExperimentManagementTests.Shared;

public abstract class ControllerTestBase<TController> where TController : ControllerBase
{
    public class TestableResponsesController : ResponsesController
    {
        public TestableResponsesController(IResponseService service, IAuthenticationService auth)
            : base(service, auth)
        {
        }
    }

    public class TestBaseApiController : BaseApiController
    {
        // Adjust the ctor to match your real BaseApiController ctor
        public TestBaseApiController(IAuthenticationService auth)
            : base(auth)
        {
        }

    }

    public static IEnumerable<object[]> ExceptionTestData =>
        new List<object[]>
        {
        new object[]
        {
            new UnauthorizedAccessException("Unauthorized!"),
            typeof(UnauthorizedObjectResult),
            401
        },
        new object[]
        {
            new AI4NGExperimentManagement.Shared.ForbiddenException("Forbidden!"),
            typeof(ObjectResult),
            403
        },
        new object[]
        {
            new Amazon.DynamoDBv2.AmazonDynamoDBException("Dynamo error"),
            typeof(ObjectResult),
            503
        },
        new object[]
        {
            new TimeoutException("Request timeout"),
            typeof(ObjectResult),
            408
        },
        new object[]
        {
            new Exception("Something failed"),
            typeof(ObjectResult),
            500
        }
        };


    protected static Mock<IAuthenticationService> CreateAuthMock(bool isResearcher = true)
    {
        var authMock = new Mock<IAuthenticationService>();
        authMock.Setup(x => x.GetUsernameFromRequest()).Returns(TestDataBuilder.TestUsername);
        authMock.Setup(x => x.IsResearcher()).Returns(isResearcher);
        return authMock;
    }

    protected static TController CreateControllerWithContext(TController controller, bool isLocal = true)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test"] = "true";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        Environment.SetEnvironmentVariable("AWS_ENDPOINT_URL", isLocal ? "http://localhost:8000" : null);

        return controller;
    }

    // New generic helper for creating controller and service mocks
    protected static (Mock<TService> mockService, TController controller, Mock<IAuthenticationService> authMock)
        CreateControllerWithMocks<TService>(Func<TService, IAuthenticationService, TController> controllerFactory, bool isLocal = true, bool isResearcher = true)
        where TService : class
    {
        var mockService = new Mock<TService>();
        var authMock = CreateAuthMock(isResearcher);
        var controller = CreateControllerWithContext(controllerFactory(mockService.Object, authMock.Object), isLocal);
        return (mockService, controller, authMock);
    }
}