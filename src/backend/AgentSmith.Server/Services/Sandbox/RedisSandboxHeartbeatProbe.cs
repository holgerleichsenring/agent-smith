using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-09-22-2d11b: reads the heartbeat key the agent writes every two seconds with a
/// ten-second expiry — the same key <see cref="SandboxLivenessWatcher"/> polls — to say
/// whether a held sandbox is still there.
/// <para>
/// One read decides it, and it tells a crash from an orderly shutdown: the agent deletes
/// the key on a clean exit, so a missing key is a sandbox that is gone either way. The
/// watcher's miss threshold is not repeated here — the watcher is guarding a run in flight
/// against a Redis hiccup, while a hold has nothing in flight and a false negative costs a
/// spawn rather than a cancelled run.
/// </para>
/// </summary>
public sealed class RedisSandboxHeartbeatProbe(
    IConnectionMultiplexer multiplexer,
    ILogger<RedisSandboxHeartbeatProbe> logger) : ISandboxHeartbeatProbe
{
    public async Task<bool> IsAliveAsync(string jobId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            return await multiplexer.GetDatabase().KeyExistsAsync(RedisKeys.HeartbeatKey(jobId));
        }
        catch (Exception ex)
        {
            // A probe that cannot read answers NO: spawning is slow and correct, and pushing
            // a step at a corpse waits out the step timeout plus a thirty-second grace.
            logger.LogWarning(ex, "Heartbeat read failed for sandbox {JobId}", jobId);
            return false;
        }
    }
}
