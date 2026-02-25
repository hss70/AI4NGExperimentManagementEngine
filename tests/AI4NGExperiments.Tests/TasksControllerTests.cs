using Microsoft.AspNetCore.Mvc;
using Moq;
using AI4NGExperimentsLambda.Controllers;
using AI4NGExperimentsLambda.Interfaces;
using AI4NGExperimentsLambda.Models;
using AI4NGExperimentManagementTests.Shared;

namespace AI4NGExperiments.Tests;

public class TasksControllerTests : ControllerTestBase<TasksController>
{
    [Fact]
    public async Task TasksController_GetAll_ShouldReturnOkWithTasks()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<ITaskService>(
            (service, auth) => new TasksController(service, auth));

        var tasks = new List<AI4NGTask> { new AI4NGTask { TaskKey = "task1", Data = new TaskData { Name = "Test Task" } } };
        mockService.Setup(x => x.GetTasksAsync()).ReturnsAsync(tasks);

        // Act
        var result = await controller.GetAll();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(tasks, okResult.Value);
    }

    [Fact]
    public async Task TasksController_GetById_ShouldReturnOkWithTask()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<ITaskService>(
            (service, auth) => new TasksController(service, auth));

        var task = new AI4NGTask { TaskKey = "task1", Data = new TaskData { Name = "Test Task" } };
        mockService.Setup(x => x.GetTaskAsync("task1")).ReturnsAsync(task);

        // Act
        var result = await controller.GetById("task1");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(task, okResult.Value);
    }

    [Fact]
    public async Task TasksController_Create_ShouldReturnOkWithResult()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<ITaskService>(
            (service, auth) => new TasksController(service, auth));

        var request = new CreateTaskRequest { TaskKey = "TEST_TASK", Data = new TaskData { Name = "Test Task" } };
        dynamic createResult = new System.Dynamic.ExpandoObject();
        ((IDictionary<string, object>)createResult)["id"] = "task1";
        mockService.Setup(x => x.CreateTaskAsync(It.IsAny<CreateTaskRequest>(), It.IsAny<string>())).ReturnsAsync((object)createResult);

        // Act
        var result = await controller.Create(request);

        // Assert
        var createdResult = Assert.IsAssignableFrom<Microsoft.AspNetCore.Mvc.ObjectResult>(result);
        Assert.Equal(201, createdResult.StatusCode);
        Assert.Equal(createResult, createdResult.Value);
    }

    [Fact]
    public async Task TasksController_Update_ShouldReturnOkWithMessage()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<ITaskService>(
            (service, auth) => new TasksController(service, auth));

        var taskData = new TaskData { Name = "Updated Task" };
        mockService.Setup(x => x.UpdateTaskAsync("task1", taskData, TestDataBuilder.TestUsername)).Returns(Task.CompletedTask);

        // Act
        var result = await controller.Update("task1", taskData);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = okResult.Value;
        Assert.NotNull(response);
        Assert.Contains("updated successfully", response.ToString());
    }

    [Fact]
    public async Task TasksController_Delete_ShouldReturnOkWithMessage()
    {
        // Arrange
        var (mockService, controller, _) = CreateControllerWithMocks<ITaskService>(
            (service, auth) => new TasksController(service, auth));

        mockService.Setup(x => x.DeleteTaskAsync("task1", TestDataBuilder.TestUsername)).Returns(Task.CompletedTask);

        // Act
        var result = await controller.Delete("task1");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = okResult.Value;
        Assert.NotNull(response);
        Assert.Contains("deleted successfully", response.ToString());
    }
}