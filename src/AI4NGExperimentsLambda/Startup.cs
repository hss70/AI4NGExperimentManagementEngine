using AI4NGExperimentsLambda.Interfaces;
using AI4NGExperimentsLambda.Services;
using AI4NGExperimentManagement.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace AI4NGExperimentsLambda;

public class Startup : BaseStartup
{
    protected override void ConfigureApplicationServices(IServiceCollection services)
    {
        services.AddScoped<IExperimentService, ExperimentService>();
    }
}