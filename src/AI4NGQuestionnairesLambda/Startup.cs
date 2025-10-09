using Amazon.DynamoDBv2;
using AI4NGQuestionnairesLambda.Interfaces;
using AI4NGQuestionnairesLambda.Services;

namespace AI4NGQuestionnairesLambda;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

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
        
        services.AddScoped<IQuestionnaireService, QuestionnaireService>();
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