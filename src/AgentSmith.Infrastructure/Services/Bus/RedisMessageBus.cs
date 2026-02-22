using AgentSmith.Infrastructure.Models;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AgentSmith.Infrastructure.Services.Bus;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Infrastructure.Services.Bus;

/// <summary>
/// Redis Streams implementation of IMessageBus.
/// Outbound stream: job:{jobId}:out  (agent → dispatcher)
/// Inbound stream:  job:{jobId}:in   (dispatcher → agent)
/// All keys use a 2-hour TTL and MAXLEN 1000 to prevent unbounded growth.
/// </summary>
public sealed class RedisMessageBus(
    IConnectionMultiplexer redis,
    ILogger<RedisMessageBus> logger) : IMessageBus
{
    private const int StreamMaxLen = 1000;
    private static readonly TimeSpan KeyTtl = TimeSpan.FromHours(2);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    public async Task PublishAsync(BusMessage message, CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        var streamKey = OutboundKey(message.JobId);
        var entries = Serialize(message);

        await db.StreamAddAsync(streamKey, entries, maxLength: StreamMaxLen, useApproximateMaxLength: true);
        await db.KeyExpireAsync(streamKey, KeyTtl);

        logger.LogDebug("Published {Type} to {Stream}", message.Type, streamKey);
    }

    public async Task PublishAnswerAsync(string jobId, string questionId, string content,
        CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        var streamKey = InboundKey(jobId);

        var answer = BusMessage.Answer(jobId, questionId, content);
        var entries = Serialize(answer);

        await db.StreamAddAsync(streamKey, entries, maxLength: StreamMaxLen, useApproximateMaxLength: true);
        await db.KeyExpireAsync(streamKey, KeyTtl);

        logger.LogDebug("Published answer for question {QuestionId} to {Stream}", questionId, streamKey);
    }

    public async IAsyncEnumerable<BusMessage> SubscribeToJobAsync(
        string jobId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
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

    public async Task<BusMessage?> ReadAnswerAsync(string jobId, TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        var streamKey = InboundKey(jobId);
        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        var lastId = "0-0";

        while (DateTimeOffset.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            var entries = await db.StreamReadAsync(streamKey, lastId, count: 1);

            if (entries is { Length: > 0 })
            {
                var entry = entries[0];
                lastId = entry.Id!;
                var message = Deserialize(jobId, entry.Values);
                if (message?.Type == BusMessageType.Answer)
                {
                    logger.LogDebug(
                        "Received answer for job {JobId}: {Content}", jobId, message.Content);
                    return message;
                }
            }

            await Task.Delay(PollInterval, cancellationToken);
        }

        logger.LogWarning("ReadAnswerAsync timed out for job {JobId}", jobId);
        return null;
    }

    public async Task CleanupJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync([OutboundKey(jobId), InboundKey(jobId)]);
        logger.LogInformation("Cleaned up streams for job {JobId}", jobId);
    }

    // --- Helpers ---

    private static RedisKey OutboundKey(string jobId) => $"job:{jobId}:out";
    private static RedisKey InboundKey(string jobId) => $"job:{jobId}:in";

    private static NameValueEntry[] Serialize(BusMessage message)
    {
        return
        [
            new NameValueEntry("type", message.Type.ToString()),
            new NameValueEntry("jobId", message.JobId),
            new NameValueEntry("text", message.Text),
            new NameValueEntry("step", message.Step?.ToString() ?? ""),
            new NameValueEntry("total", message.Total?.ToString() ?? ""),
            new NameValueEntry("questionId", message.QuestionId ?? ""),
            new NameValueEntry("prUrl", message.PrUrl ?? ""),
            new NameValueEntry("summary", message.Summary ?? ""),
            new NameValueEntry("content", message.Content ?? "")
        ];
    }

    private static BusMessage? Deserialize(string jobId, NameValueEntry[] values)
    {
        try
        {
            var dict = values.ToDictionary(e => (string)e.Name!, e => (string?)e.Value);

            if (!dict.TryGetValue("type", out var typeStr) ||
                !Enum.TryParse<BusMessageType>(typeStr, out var type))
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
                    dict.GetValueOrDefault("text") ?? ""),

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
