using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-09-22-2d11b: a heartbeat that answers the same thing every time, and records what
/// it was asked about. The probe is injectable precisely so a test — and the fast harness,
/// whose stub sandbox has neither an agent nor a Redis — can answer for it.
/// </summary>
internal sealed class StubHeartbeat(bool alive) : ISandboxHeartbeatProbe
{
    public List<string> Asked { get; } = [];

    public Task<bool> IsAliveAsync(string jobId, CancellationToken cancellationToken)
    {
        Asked.Add(jobId);
        return Task.FromResult(alive);
    }
}
