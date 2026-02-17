using AI4NGResponsesLambda.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using AI4NGExperimentManagement.Shared;
using AI4NGResponsesLambda.Controllers;

namespace AI4NGExperimentManagementTests.Shared
{
    public class HandleExceptionTests : ControllerTestBase<ResponsesController>
    {
        [Theory]
        [MemberData(nameof(ExceptionTestData))]
        public void HandleException_MapsExceptionsCorrectly(
            Exception ex,
            Type expectedResultType,
            int expectedStatusCode)
        {
            // Arrange
            var controller = new TestBaseApiController(CreateAuthMock(false).Object);

            // Act
            var result = controller.InvokeHandleException(ex, "test operation");

            // Assert – don't care about the exact subclass, just that it's some ObjectResult
            var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
            // Also verify the concrete type matches the expectation (uses expectedResultType)
            Assert.True(expectedResultType.IsAssignableFrom(objectResult.GetType()));
            Assert.Equal(expectedStatusCode, objectResult.StatusCode);

            var payloadString = objectResult.Value?.ToString();
            // payload format may change; ensure it contains an "error" key and status code matched above
            Assert.Contains("error", (payloadString ?? string.Empty).ToLower());
        }
    }

}
