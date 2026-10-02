using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Events;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// 2026-10-02-5ab2c: the database decides which runs are live; the Redis active-run set is a
/// projection of it, because the sandbox agent can read only Redis. A flush empties the set
/// while runs keep beating their rows, so the housekeeping leader rebuilds it — at start and
/// every <see cref="Interval"/> — from the fresh, unfinished, unparked rows.
/// </summary>
public sealed class ActiveRunSetReseeder(
    IConnectionMultiplexer redis,
    IRunHeartbeat heartbeat,
    ILogger<ActiveRunSetReseeder> logger)
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    // KEYS[1] the active set, KEYS[2] the recent list, ARGV the fresh run ids. A run whose
    // RunFinished already reached Redis is in the recent list while its row may still read
    // running (the broadcaster projects it a moment later); adding it back would resurrect it.
    // One script, so no RunFinished lands between the check and the add.
    internal const string ReseedScript = """
        local finished = {}
        for _, id in ipairs(redis.call('LRANGE', KEYS[2], 0, -1)) do finished[id] = true end
        local added = 0
        for _, id in ipairs(ARGV) do
          if not finished[id] then added = added + redis.call('SADD', KEYS[1], id) end
        end
        return added
        """;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try { await ReseedAsync(cancellationToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogWarning(ex, "Active-run set re-seed failed; retrying next interval"); }

            try { await Task.Delay(Interval, cancellationToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>Adds the live runs the set lacks; returns how many were added.</summary>
    public async Task<long> ReseedAsync(CancellationToken cancellationToken)
    {
        var ids = await heartbeat.GetFreshUnparkedRunIdsAsync(ActiveRunReaper.LeaseFreshFor, cancellationToken);
        if (ids.Count == 0) return 0;
        var added = (long)await redis.GetDatabase().ScriptEvaluateAsync(
            ReseedScript,
            [EventStreamKeys.ActiveRunsSet, EventStreamKeys.RecentRunsList],
            ids.Select(id => (RedisValue)id).ToArray());
        if (added > 0) logger.LogInformation("Re-seeded {Count} live run(s) into the active-run set", added);
        return added;
    }
}
