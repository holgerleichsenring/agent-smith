using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Infrastructure.Services.Lifecycle;

/// <summary>
/// Redis SETEX-backed heartbeat. Key agentsmith:heartbeat:{ticketId} is renewed
/// every RenewInterval with TTL HeartbeatTtl. StaleJobDetector reverts tickets
/// whose heartbeat key is missing back to Pending.
/// </summary>
public sealed class JobHeartbeatService(
    IConnectionMultiplexer redis,
    ILogger<JobHeartbeatService> logger) : IJobHeartbeatService
{
    private static readonly TimeSpan HeartbeatTtl = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan RenewInterval = TimeSpan.FromSeconds(30);

    public IAsyncDisposable Start(TicketId ticketId)
    {
        var key = KeyFor(ticketId);
        var cts = new CancellationTokenSource();
        var task = Task.Run(() => RenewLoopAsync(key, cts.Token));
        logger.LogDebug("Heartbeat started for ticket {Ticket}", ticketId.Value);
        return new Handle(redis, key, cts, task, logger);
    }

    public async Task<bool> IsAliveAsync(TicketId ticketId, CancellationToken cancellationToken)
        => await redis.GetDatabase().KeyExistsAsync(KeyFor(ticketId));

    private static string KeyFor(TicketId ticketId) => $"agentsmith:heartbeat:{ticketId.Value}";

    private async Task RenewLoopAsync(string key, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await db.StringSetAsync(key, DateTimeOffset.UtcNow.ToString("o"), HeartbeatTtl);
                await Task.Delay(RenewInterval, ct);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { logger.LogWarning(ex, "Heartbeat renewal failed for {Key}", key); }
        }
    }

    private sealed class Handle(
        IConnectionMultiplexer redis, string key,
        CancellationTokenSource cts, Task task,
        ILogger logger) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            cts.Cancel();
            try { await task; } catch { /* expected during cancel */ }
            try { await redis.GetDatabase().KeyDeleteAsync(key); }
            catch (Exception ex) { logger.LogDebug(ex, "Failed to clear heartbeat {Key}", key); }
            cts.Dispose();
        }
    }
}
