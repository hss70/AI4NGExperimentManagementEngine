using Amazon.DynamoDBv2;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace AI4NGExperimentManagement.Shared;

public abstract class BaseStartup
{
    public virtual void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers();
        services.AddHttpContextAccessor();
        services.AddScoped<IAuthenticationService, AuthenticationService>();

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

        // Ensure authentication is added
        services.AddAuthentication("Bearer")
            .AddJwtBearer("Bearer", options =>
            {
                options.Authority = "https://cognito-idp.eu-west-2.amazonaws.com/eu-west-2_EaNz6cSp0";
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = "https://cognito-idp.eu-west-2.amazonaws.com/eu-west-2_EaNz6cSp0",
                    ValidateAudience = true,
                    ValidAudience = "517s6c84jo5i3lqste5idb0o4c",
                    ValidateLifetime = true
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("ResearcherPolicy", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("cognito:groups", "Researcher");
            });

            options.AddPolicy("ParticipantPolicy", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("cognito:groups", "Participant");
            });
        });

        ConfigureApplicationServices(services);
    }

    public virtual void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        // Basic exception logging for Lambda to aid diagnostics
        app.Use(async (context, next) =>
        {
            try
            {
                await next();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ERROR] {context.Request.Method} {context.Request.Path}: {ex}");
                throw;
            }
        });

        app.UseRouting();
        app.UseAuthentication(); // Add this line
        app.UseAuthorization(); // And this line

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }

    protected abstract void ConfigureApplicationServices(IServiceCollection services);
}