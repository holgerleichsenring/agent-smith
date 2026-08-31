using AgentSmith.Application.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Surface;

/// <summary>
/// 2026-08-30-c6ec: reads the declared consumer checkouts and reports what they exercise,
/// with the account that bounds the reading. Null when the reading failed — which is not
/// the same answer as "these clients call nothing".
/// </summary>
public interface IClientSurfaceReader
{
    Task<ClientUsageReport?> ReadAsync(
        ClientSurfaceRequest request, PipelineCostTracker costTracker, CancellationToken cancellationToken);
}
