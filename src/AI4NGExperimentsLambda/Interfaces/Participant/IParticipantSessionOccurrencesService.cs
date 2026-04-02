using AI4NGExperimentsLambda.Models.Dtos;
using AI4NGExperimentsLambda.Models.Dtos.Responses;
using AI4NGExperimentsLambda.Models.Requests.Participant;

namespace AI4NGExperimentsLambda.Interfaces.Participant;

public interface IParticipantSessionOccurrencesService
{
    Task<IReadOnlyList<OccurrenceDto>> ListOccurrencesAsync(
        string experimentId,
        string participantId,
        string? from = null,
        string? to = null,
        CancellationToken ct = default);

    Task<OccurrenceDto?> GetOccurrenceAsync(
        string experimentId,
        string participantId,
        string occurrenceKey,
        CancellationToken ct = default);

    Task<OccurrenceDto> StartOccurrenceAsync(
        string experimentId,
        string participantId,
        string occurrenceKey,
        StartOccurrenceRequest? request = null,
        CancellationToken ct = default);

    Task<OccurrenceDto> CompleteOccurrenceAsync(
        string experimentId,
        string participantId,
        string occurrenceKey,
        CompleteOccurrenceRequest? request = null,
        CancellationToken ct = default);

    Task<OccurrenceDto> CreateOccurrenceAsync(
        string experimentId,
        string participantId,
        CreateOccurrenceRequest request,
        CancellationToken ct = default);

    Task<OccurrenceDto> SubmitTaskResponseAsync(
        string experimentId,
        string participantId,
        string occurrenceKey,
        string taskKey,
        int? taskIndex,
        SubmitTaskResponseRequest request,
        CancellationToken ct = default);

    Task<ResolveOccurrenceDto> ResolveCurrentOccurrenceAsync(
        string experimentId,
        string participantId,
        CancellationToken ct = default);
}
