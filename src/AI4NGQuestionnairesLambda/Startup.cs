using AI4NGQuestionnairesLambda.Interfaces;
using AI4NGQuestionnairesLambda.Services;
using AI4NGExperimentManagement.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace AI4NGQuestionnairesLambda;

public class Startup : BaseStartup
{
    protected override void ConfigureApplicationServices(IServiceCollection services)
    {
        services.AddScoped<IQuestionnaireService, QuestionnaireService>();
    }
}