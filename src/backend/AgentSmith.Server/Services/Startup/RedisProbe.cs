using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Contracts;
using StackExchange.Redis;

namespace AgentSmith.Server.Services.Startup;

/// <summary>
/// p0391a: is Redis reachable? Without it the job queue, the leader leases and the live
/// run feed are down — the API, the diagnostics surface and the config studio are not.
/// The multiplexer no longer aborts on a failed connect, so this probe is the place the
/// operator learns that the queue is idle because Redis is missing.
/// 2026-09-28-2ac3: the finding names the endpoints, never the probe's error text, which is
/// the client's exception message and can name the endpoint as configured.
/// </summary>
public sealed class RedisProbe(IPreflightInfraProbe infra, IConnectionMultiplexer redis) : IStartupProbe
{
    public string Subsystem => StartupSubsystems.Redis;

    public async Task<IReadOnlyList<StartupFinding>> ProbeAsync(CancellationToken cancellationToken)
    {
        var result = await infra.ProbeRedisAsync(cancellationToken);
        if (result.Ok) return [];
        return [new StartupFinding(
            StartupSubsystems.Redis,
            StartupFindingSeverity.Blocking,
            $"Redis at {RedisEndpointText.Of(redis.Configuration)} is not reachable, so the job "
            + "queue, leader election and the live run feed are down — no run can be queued or "
            + "picked up.",
            Field: "REDIS_URL")];
    }
}
