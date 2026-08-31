using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Surface;
using AgentSmith.Domain.Models;

namespace AgentSmith.Tests.Surface;

/// <summary>
/// 2026-08-30-c6ec: the reading of the client call sites, scripted. The reading itself is
/// a model call; what the difference does with its answer is not, and this is the seam
/// between the two.
/// </summary>
internal sealed class StubClientSurfaceReader(ClientUsageReport? report) : IClientSurfaceReader
{
    public ClientSurfaceRequest? Asked { get; private set; }

    public Task<ClientUsageReport?> ReadAsync(
        ClientSurfaceRequest request, PipelineCostTracker costTracker, CancellationToken cancellationToken)
    {
        Asked = request;
        return Task.FromResult(report);
    }
}
