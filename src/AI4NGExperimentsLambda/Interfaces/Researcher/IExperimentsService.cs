using AI4NGExperimentsLambda.Models;
using AI4NGExperimentsLambda.Models.Dtos;

namespace AI4NGExperimentsLambda.Interfaces.Researcher;

public interface IExperimentsService
{
    Task<IReadOnlyList<ExperimentListDto>> GetExperimentsAsync(CancellationToken ct = default);

    Task<ExperimentDto?> GetExperimentAsync(
        string experimentId,
        CancellationToken ct = default);

    Task<ValidateExperimentResponseDto> ValidateExperimentAsync(
        Experiment experiment,
        CancellationToken ct = default);

    Task<IdResponseDto> CreateExperimentAsync(
        Experiment experiment,
        string performedBy,
        CancellationToken ct = default);

    Task UpdateExperimentAsync(
        string experimentId,
        ExperimentData data,
        string performedBy,
        CancellationToken ct = default);

    Task DeleteExperimentAsync(
        string experimentId,
        string performedBy,
        CancellationToken ct = default);

    Task ActivateExperimentAsync(
        string experimentId,
        string performedBy,
        CancellationToken ct = default);

    Task PauseExperimentAsync(
        string experimentId,
        string performedBy,
        CancellationToken ct = default);

    Task CloseExperimentAsync(
        string experimentId,
        string performedBy,
        CancellationToken ct = default);
}