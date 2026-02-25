using AI4NGExperimentsLambda.Interfaces;
using AI4NGExperimentsLambda.Services;
using AI4NGExperimentManagement.Shared;
using Microsoft.Extensions.DependencyInjection;
using AI4NGExperimentManagement.Shared.Authorisation;
using AI4NGExperimentsLambda.Interfaces.Researcher;

namespace AI4NGExperimentsLambda;

public class Startup : BaseStartup
{
    protected override void ConfigureApplicationServices(IServiceCollection services)
    {
        services.AddScoped<IExperimentsService, ExperimentsService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddSingleton<IAuthorisationService, AuthorisationService>();
    }
}