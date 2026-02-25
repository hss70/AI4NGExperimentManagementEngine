using AI4NGExperimentsLambda.Models.Dtos;
using AI4NGExperimentsLambda.Models.Requests;

namespace AI4NGExperimentsLambda.Interfaces.Researcher;

public interface ISessionProtocolService
{
    Task<IEnumerable<ProtocolSessionDto>> ListProtocolSessionsAsync(
        string experimentId,
        CancellationToken ct = default);

    Task<ProtocolSessionDto?> GetProtocolSessionAsync(
        string experimentId,
        string protocolSessionKey,
        CancellationToken ct = default);

    /// <summary>
    /// Idempotent upsert for FIRST/DAILY/WEEKLY definitions.
    /// </summary>
    Task<ProtocolSessionDto> UpsertProtocolSessionAsync(
        string experimentId,
        string protocolSessionKey,
        UpsertProtocolSessionRequest request,
        string performedBy,
        CancellationToken ct = default);

    Task DeleteProtocolSessionAsync(
        string experimentId,
        string protocolSessionKey,
        string performedBy,
        CancellationToken ct = default);
}