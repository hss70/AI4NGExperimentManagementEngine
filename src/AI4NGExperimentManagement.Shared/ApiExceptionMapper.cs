using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace AI4NGExperimentManagement.Shared;

public static class ApiExceptionMapper
{
    public static IActionResult Map(Exception ex)
    {
        return ex switch
        {
            // Client errors
            ArgumentException => new BadRequestObjectResult(new { error = ex.Message }),
            KeyNotFoundException => new NotFoundObjectResult(new { error = ex.Message }),
            InvalidOperationException => new ConflictObjectResult(new { error = ex.Message }),

            // Auth
            UnauthorizedAccessException => new UnauthorizedObjectResult(new { error = ex.Message }),

            // Dynamo conditional failures
            ConditionalCheckFailedException => new ConflictObjectResult(new { error = ex.Message }),

            // Timeout
            TimeoutException => new ObjectResult(new { error = "Request timeout" })
            {
                StatusCode = 408
            },

            // Dynamo explicit throttling
            ProvisionedThroughputExceededException => Throttle(),
            RequestLimitExceededException => Throttle(),
            LimitExceededException => Throttle(),

            // Generic AWS throttling
            AmazonServiceException awsEx when IsThrottle(awsEx)
                => Throttle(),

            // Any other AWS error
            AmazonServiceException
                => new ObjectResult(new { error = "AWS service temporarily unavailable" })
                {
                    StatusCode = 503
                },

            _ => new ObjectResult(new
            {
                error = "Internal server error",
                details = IncludeDetails() ? ex.Message : null
            })
            {
                StatusCode = 500
            }
        };
    }

    private static ObjectResult Throttle()
        => new(new { error = "Service throttling, please retry" })
        { StatusCode = 503 };

    private static bool IsThrottle(AmazonServiceException ex)
    {
        var code = ex.ErrorCode ?? string.Empty;

        return code.Equals("Throttling", StringComparison.OrdinalIgnoreCase)
            || code.Equals("ThrottlingException", StringComparison.OrdinalIgnoreCase)
            || code.Equals("RequestLimitExceeded", StringComparison.OrdinalIgnoreCase)
            || code.Equals("TooManyRequestsException", StringComparison.OrdinalIgnoreCase)
            || ex.StatusCode == HttpStatusCode.TooManyRequests;
    }

    private static bool IncludeDetails()
    {
        var env = Environment.GetEnvironmentVariable("Environment")
                  ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                  ?? "prod";

        return env.Equals("dev", StringComparison.OrdinalIgnoreCase)
            || env.Equals("development", StringComparison.OrdinalIgnoreCase);
    }
}
