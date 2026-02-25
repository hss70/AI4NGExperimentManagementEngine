using Xunit;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using AI4NGExperimentsLambda.Controllers;
using AI4NGQuestionnairesLambda.Controllers;
using AI4NGResponsesLambda.Controllers;
using AI4NGExperimentsLambda.Controllers.Researcher;

namespace AI4NGExperimentManagementTests.Shared;

public class RouteIntegrationTests
{
    [Fact]
    public void ExperimentsController_ShouldHaveCorrectRouteAttributes()
    {
        // Arrange
        var controllerType = typeof(ExperimentsController);
        
        // Act & Assert - Check controller route
        var routeAttribute = controllerType.GetCustomAttribute<RouteAttribute>();
        Assert.NotNull(routeAttribute);
        Assert.Equal("api/experiments", routeAttribute.Template);
        
        // Check individual action routes
        var methods = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.DeclaringType == controllerType);
            
        var httpGetMethods = methods.Where(m => m.GetCustomAttribute<HttpGetAttribute>() != null);
        var httpPostMethods = methods.Where(m => m.GetCustomAttribute<HttpPostAttribute>() != null);
        var httpPutMethods = methods.Where(m => m.GetCustomAttribute<HttpPutAttribute>() != null);
        var httpDeleteMethods = methods.Where(m => m.GetCustomAttribute<HttpDeleteAttribute>() != null);
        
        Assert.True(httpGetMethods.Any(), "Should have GET methods");
        Assert.True(httpPostMethods.Any(), "Should have POST methods");
        Assert.True(httpPutMethods.Any(), "Should have PUT methods");
        Assert.True(httpDeleteMethods.Any(), "Should have DELETE methods");
    }

    [Fact]
    public void TasksController_ShouldHaveCorrectRouteAttributes()
    {
        // Arrange
        var controllerType = typeof(TasksController);
        
        // Act & Assert - Check controller route
        var routeAttribute = controllerType.GetCustomAttribute<RouteAttribute>();
        Assert.NotNull(routeAttribute);
        Assert.Equal("api/tasks", routeAttribute.Template);
        
        // Check individual action routes
        var methods = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.DeclaringType == controllerType);
            
        var httpGetMethods = methods.Where(m => m.GetCustomAttribute<HttpGetAttribute>() != null);
        var httpPostMethods = methods.Where(m => m.GetCustomAttribute<HttpPostAttribute>() != null);
        var httpPutMethods = methods.Where(m => m.GetCustomAttribute<HttpPutAttribute>() != null);
        var httpDeleteMethods = methods.Where(m => m.GetCustomAttribute<HttpDeleteAttribute>() != null);
        
        Assert.True(httpGetMethods.Any(), "Should have GET methods");
        Assert.True(httpPostMethods.Any(), "Should have POST methods");
        Assert.True(httpPutMethods.Any(), "Should have PUT methods");
        Assert.True(httpDeleteMethods.Any(), "Should have DELETE methods");
    }

    [Fact]
    public void QuestionnairesController_ShouldHaveCorrectRouteAttributes()
    {
        // Arrange
        var controllerType = typeof(QuestionnairesController);
        
        // Act & Assert - Check controller route
        var routeAttribute = controllerType.GetCustomAttribute<RouteAttribute>();
        Assert.NotNull(routeAttribute);
        Assert.Equal("api/[controller]", routeAttribute.Template);
        
        // Check individual action routes
        var methods = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.DeclaringType == controllerType);
            
        var httpGetMethods = methods.Where(m => m.GetCustomAttribute<HttpGetAttribute>() != null);
        var httpPostMethods = methods.Where(m => m.GetCustomAttribute<HttpPostAttribute>() != null);
        var httpPutMethods = methods.Where(m => m.GetCustomAttribute<HttpPutAttribute>() != null);
        var httpDeleteMethods = methods.Where(m => m.GetCustomAttribute<HttpDeleteAttribute>() != null);
        
        Assert.True(httpGetMethods.Any(), "Should have GET methods");
        Assert.True(httpPostMethods.Any(), "Should have POST methods");
        Assert.True(httpPutMethods.Any(), "Should have PUT methods");
        Assert.True(httpDeleteMethods.Any(), "Should have DELETE methods");
    }

    [Fact]
    public void ResponsesController_ShouldHaveCorrectRouteAttributes()
    {
        // Arrange
        var controllerType = typeof(ResponsesController);
        
        // Act & Assert - Check controller route
        var routeAttribute = controllerType.GetCustomAttribute<RouteAttribute>();
        Assert.NotNull(routeAttribute);
        Assert.Equal("api/responses", routeAttribute.Template);
        
        // Check individual action routes
        var methods = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.DeclaringType == controllerType);
            
        var httpGetMethods = methods.Where(m => m.GetCustomAttribute<HttpGetAttribute>() != null);
        var httpPostMethods = methods.Where(m => m.GetCustomAttribute<HttpPostAttribute>() != null);
        var httpPutMethods = methods.Where(m => m.GetCustomAttribute<HttpPutAttribute>() != null);
        var httpDeleteMethods = methods.Where(m => m.GetCustomAttribute<HttpDeleteAttribute>() != null);
        
        Assert.True(httpGetMethods.Any(), "Should have GET methods");
        Assert.True(httpPostMethods.Any(), "Should have POST methods");
        Assert.True(httpPutMethods.Any(), "Should have PUT methods");
        Assert.True(httpDeleteMethods.Any(), "Should have DELETE methods");
    }

    [Fact]
    public void AllControllers_ShouldInheritFromBaseApiController()
    {
        // Arrange
        var controllerTypes = new[]
        {
            typeof(ExperimentsController),
            typeof(TasksController),
            typeof(QuestionnairesController),
            typeof(ResponsesController)
        };

        // Act & Assert
        foreach (var controllerType in controllerTypes)
        {
            Assert.True(controllerType.BaseType?.Name.Contains("BaseApiController") == true,
                $"{controllerType.Name} should inherit from BaseApiController");
        }
    }

    [Theory]
    [InlineData(typeof(ExperimentsController), "GetAll")]
    [InlineData(typeof(ExperimentsController), "GetById")]
    [InlineData(typeof(ExperimentsController), "Create")]
    [InlineData(typeof(ExperimentsController), "Update")]
    [InlineData(typeof(ExperimentsController), "Delete")]
    [InlineData(typeof(TasksController), "GetAll")]
    [InlineData(typeof(TasksController), "GetById")]
    [InlineData(typeof(TasksController), "Create")]
    [InlineData(typeof(TasksController), "Update")]
    [InlineData(typeof(TasksController), "Delete")]
    [InlineData(typeof(QuestionnairesController), "GetAll")]
    [InlineData(typeof(QuestionnairesController), "GetById")]
    [InlineData(typeof(QuestionnairesController), "Create")]
    [InlineData(typeof(QuestionnairesController), "Update")]
    [InlineData(typeof(QuestionnairesController), "Delete")]
    [InlineData(typeof(ResponsesController), "GetAll")]
    [InlineData(typeof(ResponsesController), "GetById")]
    [InlineData(typeof(ResponsesController), "Create")]
    [InlineData(typeof(ResponsesController), "Update")]
    [InlineData(typeof(ResponsesController), "Delete")]
    public void Controllers_ShouldHaveRequiredCrudMethods(Type controllerType, string methodName)
    {
        // Act
        var method = controllerType.GetMethod(methodName);

        // Assert
        Assert.NotNull(method);
        Assert.True(method.IsPublic, $"{methodName} should be public");
        Assert.True(method.ReturnType == typeof(Task<IActionResult>) || 
                   method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>),
                   $"{methodName} should return Task<IActionResult> or Task<ActionResult<T>>");
    }

    [Fact]
    public void ExperimentsController_ShouldHaveExpectedEndpoints()
    {
        // Arrange
        var controllerType = typeof(ExperimentsController);

        var expectedPublicMethods = new[]
        {
            "GetAll",
            "GetById",
            "ValidateExperiment",
            "Create",
            "Update",
            "Delete",
            "Activate",
            "Pause",
            "Close"
        };

        // Act & Assert
        foreach (var methodName in expectedPublicMethods)
        {
            var method = controllerType
                .GetMethods()
                .SingleOrDefault(m => m.Name == methodName);

            Assert.NotNull(method);
            Assert.True(method!.IsPublic, $"{methodName} should be public");
        }
    }

    [Fact]
    public void QuestionnairesController_ShouldHaveBatchEndpoint()
    {
        // Arrange
        var controllerType = typeof(QuestionnairesController);

        // Act
        var batchMethod = controllerType.GetMethod("CreateBatch");

        // Assert
        Assert.NotNull(batchMethod);
        Assert.True(batchMethod.IsPublic, "CreateBatch should be public");
        
        var httpPostAttribute = batchMethod.GetCustomAttribute<HttpPostAttribute>();
        Assert.NotNull(httpPostAttribute);
        Assert.Equal("batch", httpPostAttribute.Template);
    }

    [Fact]
    public void AllControllerMethods_ShouldHaveHttpMethodAttributes()
    {
        // Arrange
        var controllerTypes = new[]
        {
            typeof(ExperimentsController),
            typeof(TasksController),
            typeof(QuestionnairesController),
            typeof(ResponsesController)
        };

        // Act & Assert
        foreach (var controllerType in controllerTypes)
        {
            var publicMethods = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.DeclaringType == controllerType && 
                           m.ReturnType.IsGenericType && 
                           m.ReturnType.GetGenericTypeDefinition() == typeof(Task<>));

            foreach (var method in publicMethods)
            {
                var hasHttpAttribute = method.GetCustomAttributes()
                    .Any(attr => attr.GetType().Name.StartsWith("Http") && 
                                attr.GetType().Name.EndsWith("Attribute"));
                
                Assert.True(hasHttpAttribute, 
                    $"{controllerType.Name}.{method.Name} should have an HTTP method attribute");
            }
        }
    }
}