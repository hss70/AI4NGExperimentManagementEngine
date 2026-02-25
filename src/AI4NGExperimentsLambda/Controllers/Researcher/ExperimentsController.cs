using Microsoft.AspNetCore.Mvc;
using AI4NGExperimentsLambda.Models;
using AI4NGExperimentManagement.Shared;
using AI4NGExperimentsLambda.Interfaces.Researcher;

namespace AI4NGExperimentsLambda.Controllers.Researcher
{
    [Route("api/experiments")]
    public class ExperimentsController : BaseApiController
    {
        private readonly IExperimentsService _experimentService;

        public ExperimentsController(IExperimentsService experimentService, IAuthenticationService authService)
            : base(authService)
        {
            _experimentService = experimentService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            RequireResearcher();
            var experiments = await _experimentService.GetExperimentsAsync(ct);
            return Ok(experiments);
        }

        [HttpGet("{experimentId}")]
        public async Task<IActionResult> GetById(string experimentId, CancellationToken ct)
        {
            RequireResearcher();
            var experiment = await _experimentService.GetExperimentAsync(experimentId, ct);
            return experiment == null ? NotFound("Experiment not found") : Ok(experiment);
        }

        [HttpPost("validate")]
        public async Task<IActionResult> ValidateExperiment([FromBody] Experiment experiment, CancellationToken ct)
        {
            RequireResearcher();
            var result = await _experimentService.ValidateExperimentAsync(experiment, ct);
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Experiment experiment, CancellationToken ct)
        {
            RequireResearcher();
            var username = GetAuthenticatedUsername();
            var result = await _experimentService.CreateExperimentAsync(experiment, username, ct);
            return Ok(result);
        }

        [HttpPut("{experimentId}")]
        public async Task<IActionResult> Update(string experimentId, [FromBody] ExperimentData data, CancellationToken ct)
        {
            RequireResearcher();
            var username = GetAuthenticatedUsername();
            await _experimentService.UpdateExperimentAsync(experimentId, data, username, ct);
            return Ok(new
            {
                message = "Experiment updated successfully"
            });
        }

        [HttpDelete("{experimentId}")]
        public async Task<IActionResult> Delete(string experimentId, CancellationToken ct)
        {
            RequireResearcher();
            var username = GetAuthenticatedUsername();
            await _experimentService.DeleteExperimentAsync(experimentId, username, ct);
            return Ok(new
            {
                message = "Experiment deleted successfully"
            });
        }

        [HttpPost("{experimentId}/activate")]
        public async Task<IActionResult> Activate(string experimentId, CancellationToken ct)
        {
            RequireResearcher();
            var username = GetAuthenticatedUsername();
            await _experimentService.ActivateExperimentAsync(experimentId, username, ct);
            return Ok(new
            {
                message = "Experiment activated successfully"
            });
        }

        [HttpPost("{experimentId}/pause")]
        public async Task<IActionResult> Pause(string experimentId, CancellationToken ct)
        {
            RequireResearcher();
            var username = GetAuthenticatedUsername();
            await _experimentService.PauseExperimentAsync(experimentId, username, ct);
            return Ok(new
            {
                message = "Experiment paused successfully"
            });
        }

        [HttpPost("{experimentId}/close")]
        public async Task<IActionResult> Close(string experimentId, CancellationToken ct)
        {
            RequireResearcher();
            var username = GetAuthenticatedUsername();
            await _experimentService.CloseExperimentAsync(experimentId, username, ct);
            return Ok(new
            {
                message = "Experiment closed successfully"
            });
        }
    }
}