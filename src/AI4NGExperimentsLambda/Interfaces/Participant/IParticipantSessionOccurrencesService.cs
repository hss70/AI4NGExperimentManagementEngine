using AI4NGExperimentsLambda.Models.Dtos;

namespace AI4NGExperimentsLambda.Interfaces.Participant;

public interface IParticipantSessionOccurrencesService
{
    Task<IEnumerable<OccurrenceDto>> ListOccurrencesAsync(
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
        CancellationToken ct = default);

    Task<OccurrenceDto> CompleteOccurrenceAsync(
        string experimentId,
        string participantId,
        string occurrenceKey,
        CancellationToken ct = default);

    // Optional (only include if you actually support it)
    Task<OccurrenceDto> RescheduleOccurrenceAsync(
        string experimentId,
        string participantId,
        string occurrenceKey,
        string newScheduledFor,
        CancellationToken ct = default);
}