using AI4NGExperimentsLambda.Models.Dtos;
using AI4NGExperimentsLambda.Models.Requests;

namespace AI4NGExperimentsLambda.Interfaces.Researcher;

public interface ISessionProtocolService
{
    Task<IReadOnlyList<ProtocolSessionDto>> GetProtocolSessionsAsync(
        string experimentId,
        CancellationToken ct = default);

    Task<ProtocolSessionDto?> GetProtocolSessionAsync(
        string experimentId,
        string protocolKey,
        CancellationToken ct = default);

    Task<ProtocolSessionDto> CreateProtocolSessionAsync(
        string experimentId,
        string protocolKey,
        UpsertProtocolSessionRequest request,
        string performedBy,
        CancellationToken ct = default);

    Task<ProtocolSessionDto> UpdateProtocolSessionAsync(
        string experimentId,
        string protocolKey,
        UpsertProtocolSessionRequest request,
        string performedBy,
        CancellationToken ct = default);

    Task DeleteProtocolSessionAsync(
        string experimentId,
        string protocolKey,
        string performedBy,
        CancellationToken ct = default);
}