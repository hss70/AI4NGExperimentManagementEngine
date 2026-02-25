using Microsoft.AspNetCore.Mvc;
using Xunit;
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
            // Act - map exception using centralized mapper
            var result = AI4NGExperimentManagement.Shared.ApiExceptionMapper.Map(ex);

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
