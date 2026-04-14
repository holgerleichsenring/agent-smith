using System.Text.Json;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Server.Services;

/// <summary>
/// Low-level Redis read/write operations for conversation state.
/// Extracted from <see cref="ConversationStateManager"/> to keep query logic separate.
/// </summary>
internal sealed class ConversationStateRepository(
    IConnectionMultiplexer redis,
    ILogger<ConversationStateRepository> logger)
{
    private static readonly TimeSpan StateTtl = TimeSpan.FromMinutes(45);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    internal async Task WriteAsync(ConversationState state)
    {
        var db = redis.GetDatabase();
        var key = BuildKey(state.Platform, state.ChannelId);
        var json = JsonSerializer.Serialize(state, JsonOptions);
        await db.StringSetAsync(key, json, StateTtl);
    }

    internal async Task<ConversationState?> ReadAsync(string platform, string channelId)
    {
        var db = redis.GetDatabase();
        var key = BuildKey(platform, channelId);
        var json = await db.StringGetAsync(key);
        return Deserialize(json, $"channel {channelId}");
    }

    internal async Task<ConversationState?> ReadByJobIndexAsync(string jobId)
    {
        var db = redis.GetDatabase();
        var indexKey = JobIndexKey(jobId);
        var channelKey = await db.StringGetAsync(indexKey);

        if (channelKey.IsNullOrEmpty)
            return null;

        var json = await db.StringGetAsync((string)channelKey!);
        return Deserialize(json, $"job {jobId}");
    }

    internal async Task IndexJobAsync(ConversationState state)
    {
        var db = redis.GetDatabase();
        var channelKey = BuildKey(state.Platform, state.ChannelId);
        await db.StringSetAsync(JobIndexKey(state.JobId), channelKey.ToString(), StateTtl);
    }

    internal async Task DeleteAsync(string platform, string channelId, string? jobId)
    {
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync(BuildKey(platform, channelId).ToString());

        if (jobId is not null)
            await db.KeyDeleteAsync(JobIndexKey(jobId).ToString());
    }

    internal async Task<IReadOnlyList<ConversationState>> ScanAllAsync(
        CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        var server = redis.GetServers().FirstOrDefault();
        if (server is null) return [];

        var states = new List<ConversationState>();

        await foreach (var key in server.KeysAsync(pattern: "conversation:*:*"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var json = await db.StringGetAsync(key);
            if (json.IsNullOrEmpty) continue;

            var state = Deserialize(json, (string)key!);
            if (state is not null) states.Add(state);
        }

        return states;
    }

    private ConversationState? Deserialize(RedisValue json, string context)
    {
        if (json.IsNullOrEmpty) return null;

        try
        {
            return JsonSerializer.Deserialize<ConversationState>(json!, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex,
                "Failed to deserialize conversation state for {Context}", context);
            return null;
        }
    }

    private static RedisKey BuildKey(string platform, string channelId) =>
        $"conversation:{platform.ToLowerInvariant()}:{channelId}";

    private static RedisKey JobIndexKey(string jobId) =>
        $"job-index:{jobId}";
}
