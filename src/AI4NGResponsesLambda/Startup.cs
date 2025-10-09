using Amazon.DynamoDBv2;
using AI4NGResponsesLambda.Interfaces;
using AI4NGResponsesLambda.Services;

namespace AI4NGResponsesLambda;

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers();
        
        var endpointUrl = Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL");
        if (!string.IsNullOrEmpty(endpointUrl))
        {
            var config = new AmazonDynamoDBConfig { ServiceURL = endpointUrl };
            services.AddSingleton<IAmazonDynamoDB>(new AmazonDynamoDBClient(config));
        }
        else
        {
            services.AddSingleton<IAmazonDynamoDB, AmazonDynamoDBClient>();
        }
        
        services.AddScoped<IResponseService, ResponseService>();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
}