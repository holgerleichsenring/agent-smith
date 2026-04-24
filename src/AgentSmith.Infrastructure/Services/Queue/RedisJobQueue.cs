using System.Runtime.CompilerServices;
using System.Text.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Infrastructure.Services.Queue;

/// <summary>
/// Redis-list-backed IRedisJobQueue. FIFO via RPUSH (enqueue at tail) + LPOP (pop head).
/// BRPOP is not exposed through StackExchange.Redis's IDatabase, so ConsumeAsync polls
/// with a short delay — matches the pattern used by RedisMessageBus.
/// </summary>
public sealed class RedisJobQueue(
    IConnectionMultiplexer redis,
    ILogger<RedisJobQueue> logger) : IRedisJobQueue
{
    private const string QueueKey = "agentsmith:queue:jobs";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    public async Task EnqueueAsync(PipelineRequest request, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        var json = JsonSerializer.Serialize(request);
        var depth = await db.ListRightPushAsync(QueueKey, json);

        logger.LogInformation(
            "Enqueued pipeline {Pipeline} for {Project}/{TicketId} (queue depth: {Depth})",
            request.PipelineName, request.ProjectName, request.TicketId?.Value, depth);
    }

    public async IAsyncEnumerable<PipelineRequest> ConsumeAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();

        while (!cancellationToken.IsCancellationRequested)
        {
            var value = await db.ListLeftPopAsync(QueueKey);
            if (value.IsNull)
            {
                if (!await DelayOrStopAsync(cancellationToken)) yield break;
                continue;
            }

            var request = TryDeserialize(value!);
            if (request is null) continue;

            yield return request;
        }
    }

    public async Task<long> LenAsync(CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        return await db.ListLengthAsync(QueueKey);
    }

    private PipelineRequest? TryDeserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<PipelineRequest>(json);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to deserialize queue item, skipping");
            return null;
        }
    }

    private static async Task<bool> DelayOrStopAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(PollInterval, ct);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
