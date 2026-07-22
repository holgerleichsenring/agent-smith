using AgentSmith.Contracts.Events;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Infrastructure.Services.Events;

/// <summary>
/// Appends events to <c>run:{runId}:events</c> with MAXLEN=10000 + 2h TTL on
/// every append. Maintains two pointer indices: SADD active on RunStarted,
/// SREM active + LPUSH+LTRIM recent on RunFinished. Indices let the
/// broadcaster cold-start without scanning the keyspace.
/// </summary>
public sealed class RedisEventPublisher(
    IConnectionMultiplexer redis,
    ILogger<RedisEventPublisher> logger) : IEventPublisher
{
    private const string PayloadField = "e";

    public async Task PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(runEvent.RunId))
            throw new InvalidOperationException(
                $"RunEvent of type {runEvent.Type} has empty RunId — events without a runId are not publishable.");

        // p0373: SandboxOutput (per-line stdout) is high-volume OPERATIONAL data and
        // must never enter the retained run stream. A build emits thousands of lines
        // that roll the MAXLEN=10000 window over within the TTL, evicting every
        // structural event — the reason the trail view collapsed mid-run. Redis is
        // ephemeral transport only; durable operational data lives in the DB
        // (system-of-record, served via /api/runs/{id}/trail). stdout is not
        // persisted per-line by design — the failure-relevant slice is captured on
        // SandboxResult.OutputTail; live line-streaming is a pull-on-demand concern.
        if (runEvent.Type == EventType.SandboxOutput)
            return;

        var db = redis.GetDatabase();
        var streamKey = EventStreamKeys.RunStream(runEvent.RunId);
        var payload = EventEnvelopeSerializer.Serialize(runEvent);

        await db.StreamAddAsync(streamKey,
            new NameValueEntry[] { new(PayloadField, payload) },
            maxLength: EventStreamKeys.StreamMaxLen,
            useApproximateMaxLength: true);
        await db.KeyExpireAsync(streamKey, EventStreamKeys.StreamTtl);

        await MaintainIndicesAsync(db, runEvent);

        logger.LogDebug("Published {Type} for run {RunId}", runEvent.Type, runEvent.RunId);
    }

    private static async Task MaintainIndicesAsync(IDatabase db, RunEvent runEvent)
    {
        switch (runEvent.Type)
        {
            case EventType.RunStarted:
                await db.SetAddAsync(EventStreamKeys.ActiveRunsSet, runEvent.RunId);
                break;
            case EventType.RunFinished:
                await db.SetRemoveAsync(EventStreamKeys.ActiveRunsSet, runEvent.RunId);
                await db.ListLeftPushAsync(EventStreamKeys.RecentRunsList, runEvent.RunId);
                await db.ListTrimAsync(EventStreamKeys.RecentRunsList, 0, EventStreamKeys.RecentRunsCap - 1);
                break;
        }
    }
}
