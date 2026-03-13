using AI4NGExperimentsLambda.Models.Dtos;
using AI4NGExperimentsLambda.Models;

namespace AI4NGExperimentsLambda.Interfaces.Researcher;

public interface IExperimentParticipantsService
{
    Task<IEnumerable<ExperimentMemberDto>> GetExperimentParticipantsAsync(
        string experimentId,
        string? cohort = null,
        string? status = null,
        string? role = null,
        CancellationToken ct = default);

    Task<ExperimentMemberDto?> GetExperimentParticipantAsync(
        string experimentId,
        string participantId,
        CancellationToken ct = default);

    Task<IdResponseDto> UpsertParticipantAsync(
        string experimentId,
        string participantId,
        ExperimentMemberRequest request,
        string performedBy,
        CancellationToken ct = default);

    Task<List<IdResponseDto>> UpsertParticipantsBatchAsync(
        string experimentId,
        IEnumerable<MemberBatchItem> participants,
        string performedBy,
        CancellationToken ct = default);

    Task RemoveParticipantAsync(
        string experimentId,
        string participantId,
        string performedBy,
        CancellationToken ct = default);
}