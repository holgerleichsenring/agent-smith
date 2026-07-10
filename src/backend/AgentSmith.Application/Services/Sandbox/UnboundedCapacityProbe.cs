using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// p0269a: default capacity probe — admits every run. Bound for InProcess / CLI /
/// test composition and any backend with no capacity concept. The real probes
/// (Kubernetes ResourceQuota, Docker concurrent-sandbox cap) live in the Server
/// layer and replace this per selected backend. Kept in Application (not
/// Infrastructure) so the shared composition can register it as the TryAdd default
/// — Application references only Contracts + Domain.
/// </summary>
public sealed class UnboundedCapacityProbe : ISandboxCapacityProbe
{
    public Task<CapacityDecision> HasCapacityAsync(RunFootprint footprint, CancellationToken cancellationToken)
        => Task.FromResult(CapacityDecision.Admit());
}
