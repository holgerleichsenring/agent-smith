using AgentSmith.Contracts.Events;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Infrastructure.Services.Events;

/// <summary>
/// Appends events to <see cref="SystemEventStreamKeys.Stream"/> with
/// MAXLEN + TTL inherited from <see cref="EventStreamKeys"/>. Parallel to
/// <see cref="RedisEventPublisher"/> but stream-keyed (no per-run fanout
/// and no active/recent indices — system events are observable through the
/// single stream and the JobsBroadcaster ring buffer).
/// </summary>
public sealed class RedisSystemEventPublisher(
    IConnectionMultiplexer redis,
    ILogger<RedisSystemEventPublisher> logger) : ISystemEventPublisher
{
    private const string PayloadField = "e";

    public async Task PublishAsync(SystemEvent systemEvent, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(systemEvent.Source))
            throw new InvalidOperationException(
                $"SystemEvent of type {systemEvent.Type} has empty Source — events without a source are not publishable.");

        var db = redis.GetDatabase();
        var streamKey = SystemEventStreamKeys.Stream;
        var payload = EventEnvelopeSerializer.SerializeSystem(systemEvent);

        await db.StreamAddAsync(streamKey,
            new NameValueEntry[] { new(PayloadField, payload) },
            maxLength: EventStreamKeys.StreamMaxLen,
            useApproximateMaxLength: true);
        await db.KeyExpireAsync(streamKey, EventStreamKeys.StreamTtl);

        logger.LogDebug("Published system {Type} from {Source}", systemEvent.Type, systemEvent.Source);
    }
}
