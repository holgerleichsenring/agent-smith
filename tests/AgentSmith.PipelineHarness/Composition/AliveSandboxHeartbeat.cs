using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.PipelineHarness.Composition;

/// <summary>
/// 2026-09-22-2d11b: the heartbeat a held sandbox is verified through, answered for the fast
/// tier. The stub sandbox runs no agent and the harness Redis is a mock, so this is the one
/// boundary a hold test must stand in for — which is why the probe is injectable at all.
/// </summary>
internal sealed class AliveSandboxHeartbeat : ISandboxHeartbeatProbe
{
    public Task<bool> IsAliveAsync(string jobId, CancellationToken cancellationToken) =>
        Task.FromResult(true);
}
