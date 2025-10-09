using Amazon.Lambda.AspNetCoreServer;

namespace AI4NGExperimentsLambda;

public class LambdaEntryPoint : APIGatewayProxyFunction
{
    protected override void Init(IWebHostBuilder builder)
    {
        builder.UseStartup<Startup>();
    }
}