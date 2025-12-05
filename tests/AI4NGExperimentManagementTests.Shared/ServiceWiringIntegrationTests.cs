using Xunit;
using Microsoft.Extensions.DependencyInjection;
using AI4NGExperimentsLambda;
using AI4NGQuestionnairesLambda;
using AI4NGResponsesLambda;
using AI4NGExperimentsLambda.Interfaces;
using AI4NGQuestionnairesLambda.Interfaces;
using AI4NGResponsesLambda.Interfaces;
using AI4NGExperimentManagement.Shared;

namespace AI4NGExperimentManagementTests.Shared;

public class ServiceWiringIntegrationTests
{
    [Fact]
    public void ExperimentsLambda_Startup_ShouldRegisterAllServices()
    {
        // Arrange
        Environment.SetEnvironmentVariable("EXPERIMENTS_TABLE", "test-experiments");
        Environment.SetEnvironmentVariable("RESPONSES_TABLE", "test-responses");
        Environment.SetEnvironmentVariable("TASKS_TABLE", "test-tasks");
        
        var services = new ServiceCollection();
        var startup = new AI4NGExperimentsLambda.Startup();

        // Act
        startup.ConfigureServices(services);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Check that all required services are registered
        Assert.NotNull(serviceProvider.GetService<IExperimentService>());
        Assert.NotNull(serviceProvider.GetService<ITaskService>());
        Assert.NotNull(serviceProvider.GetService<IAuthenticationService>());
    }

    [Fact]
    public void QuestionnairesLambda_Startup_ShouldRegisterAllServices()
    {
        // Arrange
        Environment.SetEnvironmentVariable("QUESTIONNAIRES_TABLE", "test-questionnaires");
        
        var services = new ServiceCollection();
        var startup = new AI4NGQuestionnairesLambda.Startup();

        // Act
        startup.ConfigureServices(services);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Check that all required services are registered
        Assert.NotNull(serviceProvider.GetService<IQuestionnaireService>());
        Assert.NotNull(serviceProvider.GetService<IAuthenticationService>());
    }

    [Fact]
    public void ResponsesLambda_Startup_ShouldRegisterAllServices()
    {
        // Arrange
        Environment.SetEnvironmentVariable("RESPONSES_TABLE", "test-responses");
        
        var services = new ServiceCollection();
        var startup = new AI4NGResponsesLambda.Startup();

        // Act
        startup.ConfigureServices(services);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Check that all required services are registered
        Assert.NotNull(serviceProvider.GetService<IResponseService>());
        Assert.NotNull(serviceProvider.GetService<IAuthenticationService>());
    }

    [Fact]
    public void ExperimentsLambda_Services_ShouldBeRegisteredWithCorrectLifetime()
    {
        // Arrange
        Environment.SetEnvironmentVariable("EXPERIMENTS_TABLE", "test-experiments");
        Environment.SetEnvironmentVariable("RESPONSES_TABLE", "test-responses");
        Environment.SetEnvironmentVariable("TASKS_TABLE", "test-tasks");
        
        var services = new ServiceCollection();
        var startup = new AI4NGExperimentsLambda.Startup();

        // Act
        startup.ConfigureServices(services);

        // Assert - Check service lifetimes
        var experimentServiceDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IExperimentService));
        var taskServiceDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(ITaskService));

        Assert.NotNull(experimentServiceDescriptor);
        Assert.NotNull(taskServiceDescriptor);
        Assert.Equal(ServiceLifetime.Scoped, experimentServiceDescriptor.Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, taskServiceDescriptor.Lifetime);
    }

    [Fact]
    public void QuestionnairesLambda_Services_ShouldBeRegisteredWithCorrectLifetime()
    {
        // Arrange
        Environment.SetEnvironmentVariable("QUESTIONNAIRES_TABLE", "test-questionnaires");
        
        var services = new ServiceCollection();
        var startup = new AI4NGQuestionnairesLambda.Startup();

        // Act
        startup.ConfigureServices(services);

        // Assert - Check service lifetimes
        var questionnaireServiceDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IQuestionnaireService));

        Assert.NotNull(questionnaireServiceDescriptor);
        Assert.Equal(ServiceLifetime.Scoped, questionnaireServiceDescriptor.Lifetime);
    }

    [Fact]
    public void ResponsesLambda_Services_ShouldBeRegisteredWithCorrectLifetime()
    {
        // Arrange
        Environment.SetEnvironmentVariable("RESPONSES_TABLE", "test-responses");
        
        var services = new ServiceCollection();
        var startup = new AI4NGResponsesLambda.Startup();

        // Act
        startup.ConfigureServices(services);

        // Assert - Check service lifetimes
        var responseServiceDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IResponseService));

        Assert.NotNull(responseServiceDescriptor);
        Assert.Equal(ServiceLifetime.Scoped, responseServiceDescriptor.Lifetime);
    }

    [Fact]
    public void AllStartups_ShouldInheritFromBaseStartup()
    {
        // Arrange
        var startupTypes = new[]
        {
            typeof(AI4NGExperimentsLambda.Startup),
            typeof(AI4NGQuestionnairesLambda.Startup),
            typeof(AI4NGResponsesLambda.Startup)
        };

        // Act & Assert
        foreach (var startupType in startupTypes)
        {
            Assert.True(startupType.BaseType == typeof(BaseStartup),
                $"{startupType.Name} should inherit from BaseStartup");
        }
    }

    [Fact]
    public void BaseStartup_ShouldRegisterCommonServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var startup = new TestableStartup();

        // Act
        startup.ConfigureServices(services);

        // Assert - Check that base services are registered
        Assert.NotNull(services.FirstOrDefault(s => s.ServiceType == typeof(IAuthenticationService)));
    }

    [Fact]
    public void ServiceRegistration_ShouldNotHaveDuplicates()
    {
        // Arrange
        Environment.SetEnvironmentVariable("EXPERIMENTS_TABLE", "test-experiments");
        Environment.SetEnvironmentVariable("RESPONSES_TABLE", "test-responses");
        Environment.SetEnvironmentVariable("TASKS_TABLE", "test-tasks");
        
        var services = new ServiceCollection();
        var startup = new AI4NGExperimentsLambda.Startup();

        // Act
        startup.ConfigureServices(services);

        // Assert - Check for duplicate service registrations (excluding framework services)
        var serviceTypes = services.Where(s => s.ServiceType.Namespace?.StartsWith("AI4NG") == true)
                                  .Select(s => s.ServiceType).ToList();
        var duplicates = serviceTypes.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key);

        Assert.Empty(duplicates);
    }

    [Fact]
    public void ServiceRegistration_ShouldResolveWithoutCircularDependencies()
    {
        // Arrange
        Environment.SetEnvironmentVariable("EXPERIMENTS_TABLE", "test-experiments");
        Environment.SetEnvironmentVariable("RESPONSES_TABLE", "test-responses");
        Environment.SetEnvironmentVariable("TASKS_TABLE", "test-tasks");
        
        var services = new ServiceCollection();
        var startup = new AI4NGExperimentsLambda.Startup();

        // Act
        startup.ConfigureServices(services);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Should not throw when resolving services
        var experimentService = serviceProvider.GetService<IExperimentService>();
        var taskService = serviceProvider.GetService<ITaskService>();
        var authService = serviceProvider.GetService<IAuthenticationService>();

        Assert.NotNull(experimentService);
        Assert.NotNull(taskService);
        Assert.NotNull(authService);
    }

    [Theory]
    [InlineData(typeof(AI4NGExperimentsLambda.Startup))]
    [InlineData(typeof(AI4NGQuestionnairesLambda.Startup))]
    [InlineData(typeof(AI4NGResponsesLambda.Startup))]
    public void Startup_ConfigureServices_ShouldNotThrow(Type startupType)
    {
        // Arrange
        Environment.SetEnvironmentVariable("EXPERIMENTS_TABLE", "test-experiments");
        Environment.SetEnvironmentVariable("RESPONSES_TABLE", "test-responses");
        Environment.SetEnvironmentVariable("TASKS_TABLE", "test-tasks");
        Environment.SetEnvironmentVariable("QUESTIONNAIRES_TABLE", "test-questionnaires");
        
        var services = new ServiceCollection();
        var startup = Activator.CreateInstance(startupType);
        var configureMethod = startupType.GetMethod("ConfigureServices");

        // Act & Assert - Should not throw
        var exception = Record.Exception(() => configureMethod?.Invoke(startup, new object[] { services }));
        Assert.Null(exception);
    }

    // Helper class to test BaseStartup directly
    private class TestableStartup : BaseStartup
    {
        protected override void ConfigureApplicationServices(IServiceCollection services)
        {
            // No additional services for testing
        }
    }
}