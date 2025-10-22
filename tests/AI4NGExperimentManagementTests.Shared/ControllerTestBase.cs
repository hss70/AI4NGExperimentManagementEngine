using AI4NGExperimentManagement.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace AI4NGExperimentManagementTests.Shared;

public abstract class ControllerTestBase<TController> where TController : ControllerBase
{
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