using Amazon.Lambda.AspNetCoreServer;

namespace AI4NGResponsesLambda;

public class LambdaEntryPoint : APIGatewayProxyFunction
{
    protected override void Init(IWebHostBuilder builder)
    {
        builder.UseStartup<Startup>();
    }
}