using System.Runtime.CompilerServices;
using AgentSmith.Infrastructure.Models;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Server.Services;

/// <summary>
/// p0169b: subscribes to a job's outbound Redis Stream for SSE delivery.
/// Distinct from IMessageBus.SubscribeToJobAsync — JobStreamEndpoint needs
/// to choose between live-from-now (default) and replay-from-beginning
/// (operator's "I missed the run" affordance). The existing IMessageBus
/// path always replays from "0-0", which is wrong for the SSE default
/// where operators expect "live from now".
/// </summary>
public interface IJobBusSubscriber
{
    IAsyncEnumerable<BusMessage> SubscribeAsync(
        string jobId, bool fromBeginning, CancellationToken cancellationToken);
}

public sealed class JobBusSubscriber(
    IConnectionMultiplexer redis,
    ILogger<JobBusSubscriber> logger) : IJobBusSubscriber
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    public async IAsyncEnumerable<BusMessage> SubscribeAsync(
        string jobId, bool fromBeginning,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        var streamKey = (RedisKey)$"job:{jobId}:out";
        var lastId = fromBeginning ? "0-0" : await ResolveLatestIdAsync(db, streamKey);

        logger.LogInformation(
            "SSE subscribe job={Job} from_beginning={Replay} start={Start}",
            jobId, fromBeginning, lastId);

        while (!cancellationToken.IsCancellationRequested)
        {
            var entries = await db.StreamReadAsync(streamKey, lastId, count: 10);
            if (entries is null || entries.Length == 0)
            {
                try { await Task.Delay(PollInterval, cancellationToken); }
                catch (OperationCanceledException) { yield break; }
                continue;
            }

            foreach (var entry in entries)
            {
                lastId = entry.Id!;
                var message = MessageBusEntrySerializer.TryDeserialize(jobId, entry.Values);
                if (message is null) continue;
                yield return message;
                if (message.Type is BusMessageType.Done or BusMessageType.Error) yield break;
            }
        }
    }

    private static async Task<string> ResolveLatestIdAsync(IDatabase db, RedisKey key)
    {
        try
        {
            var info = await db.StreamInfoAsync(key);
            return info.LastEntry.Id.HasValue ? info.LastEntry.Id.ToString() : "0-0";
        }
        catch (RedisServerException)
        {
            // Stream doesn't exist yet — start from 0 so the first published
            // message lands in the consumer.
            return "0-0";
        }
    }
}
