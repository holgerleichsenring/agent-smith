using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-09-22-2d11b: the default heartbeat probe — no Redis, so no heartbeat can be
/// read and no held sandbox is ever verified. The server composition replaces it with
/// the Redis probe; every other composition keeps this one, takes no hold back and
/// pays the spawn it always paid.
/// </summary>
public sealed class NoSandboxHeartbeatProbe : ISandboxHeartbeatProbe
{
    public Task<bool> IsAliveAsync(string jobId, CancellationToken cancellationToken)
    {
        _ = jobId;
        _ = cancellationToken;
        return Task.FromResult(false);
    }
}
