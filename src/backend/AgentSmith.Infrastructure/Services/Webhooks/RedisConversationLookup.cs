using System.Text.Json;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Infrastructure.Services.Webhooks;

/// <summary>
/// Redis-based implementation of <see cref="IConversationLookup"/>.
/// Reads the same conversation state keys written by ConversationStateManager in Dispatcher.
/// Key schema: conversation:{platform}:{channelId}
/// PR channel format: pr:{repoFullName}#{prIdentifier}
/// </summary>
public sealed class RedisConversationLookup(
    IConnectionMultiplexer redis,
    ILogger<RedisConversationLookup> logger) : IConversationLookup
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<ConversationLookupResult?> FindByPrAsync(
        string platform, string repoFullName, string prIdentifier,
        CancellationToken cancellationToken)
    {
        var channelId = $"pr:{repoFullName}#{prIdentifier}";
        var key = $"conversation:{platform.ToLowerInvariant()}:{channelId}";

        var db = redis.GetDatabase();
        var json = await db.StringGetAsync(key);

        if (json.IsNullOrEmpty)
        {
            logger.LogDebug(
                "No conversation state found for {Platform}:{ChannelId}", platform, channelId);
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse((string)json!);
            var root = doc.RootElement;

            var jobId = root.GetProperty("jobId").GetString() ?? "";
            var storedChannelId = root.GetProperty("channelId").GetString() ?? channelId;
            var pendingQuestionId = root.TryGetProperty("pendingQuestionId", out var pq)
                ? pq.GetString()
                : null;

            return new ConversationLookupResult(jobId, storedChannelId, pendingQuestionId);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex,
                "Failed to deserialize conversation state for key {Key}", key);
            return null;
        }
    }
}
