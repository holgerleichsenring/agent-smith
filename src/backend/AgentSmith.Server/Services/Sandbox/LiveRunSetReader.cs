using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Events;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// p0242: the run ids a reaper must treat as alive — the volatile Redis active-run
/// set UNIONED with the flush-proof DB lease. An empty/flushed Redis is not 'all runs
/// dead': a live run renews its DB heartbeat, so its id stays here regardless.
/// p0465 extracted it from the two reapers that had grown their own copy.
/// 2026-10-02-75dc: a run with no lease (init) is proven alive by its own row beat, under
/// the same freshness — both signals mean "a process is driving this run".
/// </summary>
public sealed class LiveRunSetReader(
    IConnectionMultiplexer redis,
    IActiveRunLease activeRunLease,
    IRunHeartbeat runHeartbeat,
    ILogger<LiveRunSetReader> logger)
{
    public static readonly TimeSpan LeaseFreshFor = TimeSpan.FromMinutes(3);

    public async Task<ISet<string>> ReadAsync(CancellationToken cancellationToken)
    {
        var live = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            var members = await redis.GetDatabase().SetMembersAsync(EventStreamKeys.ActiveRunsSet);
            foreach (var member in members) live.Add((string)member!);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not read the Redis active-runs set — DB lease and run beats only");
        }
        foreach (var runId in await activeRunLease.GetActiveRunIdsAsync(LeaseFreshFor, cancellationToken))
            live.Add(runId);
        foreach (var runId in await runHeartbeat.GetFreshRunIdsAsync(LeaseFreshFor, cancellationToken))
            live.Add(runId);
        return live;
    }
}
