using AI4NGExperimentsLambda.Interfaces;
using AI4NGExperimentsLambda.Services.Researcher;
using AI4NGExperimentManagement.Shared;
using Microsoft.Extensions.DependencyInjection;
using AI4NGExperimentManagement.Shared.Authorisation;
using AI4NGExperimentsLambda.Interfaces.Researcher;
using AI4NGExperimentsLambda.Interfaces.Participant;
using Amazon.CognitoIdentityProvider;
using Amazon.Extensions.NETCore.Setup;
using AI4NGExperimentsLambda.Services.Participant;

namespace AI4NGExperimentsLambda;

public class Startup : BaseStartup
{
    protected override void ConfigureApplicationServices(IServiceCollection services)
    {
        services.AddScoped<IExperimentsService, ExperimentsService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddSingleton<IAuthorisationService, AuthorisationService>();
        services.AddScoped<ISessionProtocolService, SessionProtocolService>();
        services.AddScoped<IExperimentParticipantsService, ExperimentParticipantsService>();
        services.AddAWSService<IAmazonCognitoIdentityProvider>();
        services.AddScoped<IUserLookupService, UserLookupService>();
        services.AddScoped<IParticipantExperimentsService, ParticipantExperimentsService>();
        services.AddScoped<IParticipantSessionOccurrencesService, ParticipantSessionOccurrencesService>();
    }
}