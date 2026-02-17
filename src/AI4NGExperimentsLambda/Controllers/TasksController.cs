using Microsoft.AspNetCore.Mvc;
using AI4NGExperimentsLambda.Interfaces;
using AI4NGExperimentsLambda.Models;
using AI4NGExperimentManagement.Shared;

namespace AI4NGExperimentsLambda.Controllers
{
    /// <summary>
    /// Controller for managing tasks (researcher-only).
    /// </summary>
    [Route("api/tasks")]
    public class TasksController : BaseApiController
    {
        private readonly ITaskService _taskService;

        public TasksController(ITaskService taskService, IAuthenticationService authService)
            : base(authService)
        {
            _taskService = taskService;
        }

        /// <summary>
        /// Retrieves all tasks (researcher-only).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                RequireResearcher();
                var tasks = await _taskService.GetTasksAsync();
                return Ok(tasks);
            }
            catch (Exception ex)
            {
                return HandleException(ex, "retrieving tasks");
            }
        }

        /// <summary>
        /// Retrieves a specific task by ID (researcher-only).
        /// </summary>
        [HttpGet("{taskKey}")]
        public async Task<IActionResult> GetById(string taskKey)
        {
            try
            {
                RequireResearcher();
                var task = await _taskService.GetTaskAsync(taskKey);
                return task == null ? NotFound("Task not found") : Ok(task);
            }
            catch (Exception ex)
            {
                return HandleException(ex, "retrieving task");
            }
        }

        /// <summary>
        /// Creates a new task (researcher-only).
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateTaskRequest request)
        {
            try
            {
                RequireResearcher();
                var username = GetAuthenticatedUsername();
                var result = await _taskService.CreateTaskAsync(request, username);
                return Ok(result);
            }
            catch (ArgumentException ex) when (ex.Message.Contains("Missing questionnaires"))
            {
                var missing = ex.Message.Replace("Missing questionnaires: ", "").Split(", ");
                return BadRequest(new { error = "ValidationError", missingQuestionnaires = missing });
            }
            catch (Exception ex)
            {
                return HandleException(ex, "creating task");
            }
        }

        /// <summary>
        /// Updates an existing task (researcher-only).
        /// </summary>
        [HttpPut("{taskKey}")]
        public async Task<IActionResult> Update(string taskKey, [FromBody] TaskData data)
        {
            try
            {
                RequireResearcher();
                var username = GetAuthenticatedUsername();
                await _taskService.UpdateTaskAsync(taskKey, data, username);
                return Ok(new { message = "Task updated successfully" });
            }
            catch (Exception ex)
            {
                return HandleException(ex, "updating task");
            }
        }

        /// <summary>
        /// Deletes a task (researcher-only).
        /// </summary>
        [HttpDelete("{taskKey}")]
        public async Task<IActionResult> Delete(string taskKey)
        {
            try
            {
                RequireResearcher();
                var username = GetAuthenticatedUsername();
                await _taskService.DeleteTaskAsync(taskKey, username);
                return Ok(new { message = "Task deleted successfully" });
            }
            catch (Exception ex)
            {
                return HandleException(ex, "deleting task");
            }
        }
    }
}