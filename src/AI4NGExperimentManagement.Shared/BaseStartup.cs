using Amazon.DynamoDBv2;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

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

        services.AddAuthentication("Bearer")
            .AddJwtBearer("Bearer", options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = "https://cognito-idp.eu-west-2.amazonaws.com/eu-west-2_EaNz6cSp0",
                    ValidateAudience = false,
                    ValidAudience = "517s6c84jo5i3lqste5idb0o4c", // Cognito App Client ID
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeyResolver = (token, securityToken, kid, validationParameters) =>
                    {
                        var assembly = typeof(BaseStartup).Assembly;
                        using var stream = assembly.GetManifestResourceStream(
                            "AI4NGExperimentManagement.Shared.Resources.jwks.json");
                        if (stream == null)
                            throw new FileNotFoundException("Embedded JWKS resource not found.");

                        using var reader = new StreamReader(stream);
                        var jwksJson = reader.ReadToEnd();
                        var jwks = new JsonWebKeySet(jwksJson);
                        return jwks.Keys;
                    },
                    NameClaimType = "cognito:username",
                    RoleClaimType = "cognito:groups",
                };

                //Fallback logic for tokens that use "username" instead of "cognito:username"
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        var principal = context.Principal;
                        var identity = principal?.Identity as ClaimsIdentity;
                        var usernameClaim = identity?.FindFirst("cognito:username") ??
                                            identity?.FindFirst("username");
                        if (identity != null && usernameClaim != null)
                        {
                            // Add a standard Name claim so User.Identity.Name is always populated
                            identity.AddClaim(new Claim(ClaimTypes.Name, usernameClaim.Value));
                        }
                        return Task.CompletedTask;
                    }
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