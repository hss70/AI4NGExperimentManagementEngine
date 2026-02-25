using AI4NGExperimentsLambda.Models.Dtos;

namespace AI4NGExperimentsLambda.Interfaces.Participant;

public interface IParticipantExperimentsService
{
    Task<IEnumerable<ExperimentListDto>> GetMyExperimentsAsync(
        string participantId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the participant-facing bundle (experiment + protocol + tasks + maybe occurrence summaries).
    /// You can keep returning ExperimentSyncDto for now.
    /// </summary>
    Task<ExperimentSyncDto> GetExperimentBundleAsync(
        string experimentId,
        string participantId,
        DateTime? since = null,
        CancellationToken ct = default);
}