using AgentSmith.Infrastructure.Models;
using System.Runtime.CompilerServices;
using AgentSmith.Infrastructure.Services.Bus;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Infrastructure.Services.Bus;

/// <summary>
/// Redis Streams implementation of IMessageBus: reads a job's outbound stream
/// (job:{jobId}:out), which its writers bound with a TTL and a MAXLEN.
/// </summary>
public sealed class RedisMessageBus(
    IConnectionMultiplexer redis,
    ILogger<RedisMessageBus> logger) : IMessageBus
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    public async IAsyncEnumerable<BusMessage> SubscribeToJobAsync(
        string jobId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        var streamKey = OutboundKey(jobId);
        var lastId = "0-0";

        logger.LogInformation("Subscribing to job stream {Stream}", streamKey);

        while (!cancellationToken.IsCancellationRequested)
        {
            var entries = await db.StreamReadAsync(streamKey, lastId, count: 10);

            if (entries is null || entries.Length == 0)
            {
                await Task.Delay(PollInterval, cancellationToken);
                continue;
            }

            foreach (var entry in entries)
            {
                lastId = entry.Id!;
                var message = Deserialize(jobId, entry.Values);
                if (message is null) continue;

                yield return message;

                if (message.Type is BusMessageType.Done or BusMessageType.Error)
                {
                    logger.LogInformation(
                        "Job {JobId} stream ended with {Type}", jobId, message.Type);
                    yield break;
                }
            }
        }
    }

    // --- Helpers ---

    private static RedisKey OutboundKey(string jobId) => $"job:{jobId}:out";

    private static BusMessage? Deserialize(string jobId, NameValueEntry[] values)
    {
        try
        {
            var dict = values.ToDictionary(e => (string)e.Name!, e => (string?)e.Value);

            // p0315b: ignoreCase — RedisDialogueTransport writes its type field
            // lowercase ("question") on the SAME stream this bus reads, so a
            // case-sensitive parse silently dropped every ask_human question.
            if (!dict.TryGetValue("type", out var typeStr) ||
                !Enum.TryParse<BusMessageType>(typeStr, ignoreCase: true, out var type))
                return null;

            return type switch
            {
                BusMessageType.Progress => BusMessage.Progress(
                    jobId,
                    int.TryParse(dict.GetValueOrDefault("step"), out var s) ? s : 0,
                    int.TryParse(dict.GetValueOrDefault("total"), out var t) ? t : 0,
                    dict.GetValueOrDefault("text") ?? ""),

                BusMessageType.Question => BusMessage.Question(
                    jobId,
                    dict.GetValueOrDefault("questionId") ?? "",
                    dict.GetValueOrDefault("text") ?? ""),

                BusMessageType.Done => BusMessage.Done(
                    jobId,
                    dict.GetValueOrDefault("prUrl"),
                    dict.GetValueOrDefault("summary") ?? ""),

                BusMessageType.Error => BusMessage.Error(
                    jobId,
                    dict.GetValueOrDefault("text") ?? "", 0, 0, string.Empty),

                BusMessageType.Answer => BusMessage.Answer(
                    jobId,
                    dict.GetValueOrDefault("questionId") ?? "",
                    dict.GetValueOrDefault("content") ?? ""),

                _ => null
            };
        }
        catch (Exception ex)
        {
            // Swallow deserialization errors - bad messages are skipped
            _ = ex;
            return null;
        }
    }
}
