using AI4NGResponsesLambda.Interfaces;
using AI4NGResponsesLambda.Services;
using AI4NGExperimentManagement.Shared;
using Microsoft.Extensions.DependencyInjection;
using AI4NGExperimentManagement.Shared.Authorisation;

namespace AI4NGResponsesLambda;

public class Startup : BaseStartup
{
    protected override void ConfigureApplicationServices(IServiceCollection services)
    {
        services.AddScoped<IResponseService, ResponseService>();
        services.AddSingleton<IAuthorisationService, AuthorisationService>();
    }
}