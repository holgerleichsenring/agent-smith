using System.Text.Json;
using AgentSmith.Dispatcher.Models;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Dispatcher.Services;

/// <summary>
/// Manages pending clarifications in Redis. Separate from ConversationStateManager
/// because clarifications exist before a job is spawned.
/// Key: clarification:{platform}:{channelId} — TTL 2 hours.
/// </summary>
public sealed class ClarificationStateManager(
    IConnectionMultiplexer redis,
    ILogger<ClarificationStateManager> logger)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(2);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task SetAsync(
        string platform, string channelId, PendingClarification pending,
        CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        var key = BuildKey(platform, channelId);
        var json = JsonSerializer.Serialize(pending, JsonOptions);
        await db.StringSetAsync(key, json, Ttl);

        logger.LogDebug("Stored pending clarification for {ChannelId}", channelId);
    }

    public async Task<PendingClarification?> GetAsync(
        string platform, string channelId,
        CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        var json = await db.StringGetAsync(BuildKey(platform, channelId));

        if (json.IsNullOrEmpty) return null;

        try
        {
            return JsonSerializer.Deserialize<PendingClarification>(json!, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task ClearAsync(
        string platform, string channelId,
        CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync(BuildKey(platform, channelId).ToString());
        logger.LogDebug("Cleared pending clarification for {ChannelId}", channelId);
    }

    private static RedisKey BuildKey(string platform, string channelId) =>
        $"clarification:{platform.ToLowerInvariant()}:{channelId}";
}
