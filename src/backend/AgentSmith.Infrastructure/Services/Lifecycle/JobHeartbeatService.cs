using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Infrastructure.Services.Lifecycle;

/// <summary>
/// Redis SETEX-backed heartbeat. Key agentsmith:heartbeat:{ticketId} is renewed
/// every RenewInterval with TTL HeartbeatTtl, its VALUE the owning run id.
/// StaleJobDetector reverts tickets whose heartbeat key is missing back to Pending.
///
/// p0238: two correctness fixes over the original per-ticket-only design —
///  (1) the value is the run id and dispose is a compare-and-delete, so a finishing
///      run can never clear a different run's heartbeat for the same ticket;
///  (2) MarkClaimedAsync writes the key at claim time, bridging the Enqueued→
///      InProgress window the stale detector used to mistake for a dead run.
/// </summary>
public sealed class JobHeartbeatService(
    IConnectionMultiplexer redis,
    ILogger<JobHeartbeatService> logger) : IJobHeartbeatService
{
    private static readonly TimeSpan HeartbeatTtl = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan RenewInterval = TimeSpan.FromSeconds(30);

    // Delete only when the stored value is still our run id (mirrors RedisClaimLock).
    private const string CompareAndDeleteScript =
        "if redis.call('GET', KEYS[1]) == ARGV[1] then return redis.call('DEL', KEYS[1]) else return 0 end";

    public IAsyncDisposable Start(TicketId ticketId, string runId)
    {
        var key = KeyFor(ticketId);
        var cts = new CancellationTokenSource();
        var task = RenewLoopAsync(key, runId, cts.Token);
        logger.LogDebug("Heartbeat started for ticket {Ticket} run {Run}", ticketId.Value, runId);
        return new Handle(redis, key, runId, cts, task, logger);
    }

    public async Task MarkClaimedAsync(TicketId ticketId, CancellationToken cancellationToken)
    {
        // The bridge marker is not owned by a run yet — Start's renewal overwrites
        // the value with the real run id once the job begins executing.
        await redis.GetDatabase().StringSetAsync(KeyFor(ticketId), "claimed", HeartbeatTtl);
    }

    public async Task<bool> IsAliveAsync(TicketId ticketId, CancellationToken cancellationToken)
        => await redis.GetDatabase().KeyExistsAsync(KeyFor(ticketId));

    private static string KeyFor(TicketId ticketId) => $"agentsmith:heartbeat:{ticketId.Value}";

    private async Task RenewLoopAsync(string key, string runId, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await db.StringSetAsync(key, runId, HeartbeatTtl);
                await Task.Delay(RenewInterval, ct);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { logger.LogWarning(ex, "Heartbeat renewal failed for {Key}", key); }
        }
    }

    private sealed class Handle(
        IConnectionMultiplexer redis, string key, string runId,
        CancellationTokenSource cts, Task task,
        ILogger logger) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            cts.Cancel();
            try { await task; } catch { /* expected during cancel */ }
            try
            {
                // p0238: compare-and-delete — only clear the heartbeat if it is
                // still THIS run's. A concurrent run (or a re-claim that already
                // overwrote the value) keeps its own liveness.
                await redis.GetDatabase().ScriptEvaluateAsync(
                    CompareAndDeleteScript, [key], [runId]);
            }
            catch (Exception ex) { logger.LogDebug(ex, "Failed to clear heartbeat {Key}", key); }
            cts.Dispose();
        }
    }
}
