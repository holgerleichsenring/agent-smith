using System.Text.Json;
using AgentSmith.Dispatcher.Models;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Dispatcher.Services;

/// <summary>
/// Manages active conversation state in Redis.
/// Maps a chat channel to its currently running K8s job.
/// Key schema: conversation:{platform}:{channelId}
/// TTL: 2 hours (auto-cleanup after job ends or times out).
/// </summary>
public sealed class ConversationStateManager(
    IConnectionMultiplexer redis,
    ILogger<ConversationStateManager> logger)
{
    private static readonly TimeSpan StateTtl = TimeSpan.FromHours(2);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Persists a new conversation state for the given channel.
    /// Overwrites any existing state (one active job per channel).
    /// </summary>
    public async Task SetAsync(ConversationState state, CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        var key = BuildKey(state.Platform, state.ChannelId);
        var json = JsonSerializer.Serialize(state, JsonOptions);

        await db.StringSetAsync(key, json, StateTtl);

        logger.LogInformation(
            "Stored conversation state for channel {ChannelId} on {Platform}: job {JobId}",
            state.ChannelId, state.Platform, state.JobId);
    }

    /// <summary>
    /// Retrieves the active conversation state for a channel.
    /// Returns null if no active job exists for the channel.
    /// </summary>
    public async Task<ConversationState?> GetAsync(string platform, string channelId,
        CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        var key = BuildKey(platform, channelId);
        var json = await db.StringGetAsync(key);

        if (json.IsNullOrEmpty)
            return null;

        try
        {
            return JsonSerializer.Deserialize<ConversationState>(json!, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex,
                "Failed to deserialize conversation state for channel {ChannelId}", channelId);
            return null;
        }
    }

    /// <summary>
    /// Retrieves conversation state by job ID (reverse lookup via scan).
    /// Used when the dispatcher receives a bus message and needs the channel.
    /// Prefer GetByJobIdIndexAsync if high throughput is needed.
    /// </summary>
    public async Task<ConversationState?> GetByJobIdAsync(string jobId,
        CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        var indexKey = JobIndexKey(jobId);
        var channelKey = await db.StringGetAsync(indexKey);

        if (channelKey.IsNullOrEmpty)
            return null;

        var json = await db.StringGetAsync((string)channelKey!);

        if (json.IsNullOrEmpty)
            return null;

        try
        {
            return JsonSerializer.Deserialize<ConversationState>(json!, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex,
                "Failed to deserialize conversation state for job {JobId}", jobId);
            return null;
        }
    }

    /// <summary>
    /// Updates the pending question on an existing conversation state.
    /// </summary>
    public async Task SetPendingQuestionAsync(string platform, string channelId,
        string questionId, CancellationToken cancellationToken = default)
    {
        var existing = await GetAsync(platform, channelId, cancellationToken);
        if (existing is null)
        {
            logger.LogWarning(
                "Cannot set pending question: no state for channel {ChannelId}", channelId);
            return;
        }

        await SetAsync(existing.WithPendingQuestion(questionId), cancellationToken);
    }

    /// <summary>
    /// Clears the pending question from an existing conversation state.
    /// </summary>
    public async Task ClearPendingQuestionAsync(string platform, string channelId,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetAsync(platform, channelId, cancellationToken);
        if (existing is null) return;

        await SetAsync(existing.ClearPendingQuestion(), cancellationToken);
    }

    /// <summary>
    /// Removes all state for a channel after the job completes.
    /// Also removes the job-id index entry.
    /// </summary>
    public async Task RemoveAsync(string platform, string channelId,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetAsync(platform, channelId, cancellationToken);

        var db = redis.GetDatabase();
        var key = BuildKey(platform, channelId);
        await db.KeyDeleteAsync(key.ToString());

        if (existing is not null)
            await db.KeyDeleteAsync(JobIndexKey(existing.JobId).ToString());

        logger.LogInformation(
            "Removed conversation state for channel {ChannelId} on {Platform}",
            channelId, platform);
    }

    /// <summary>
    /// Stores a secondary index: jobId → channel key.
    /// Must be called alongside SetAsync to enable GetByJobIdAsync.
    /// </summary>
    public async Task IndexJobAsync(ConversationState state,
        CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        var channelKey = BuildKey(state.Platform, state.ChannelId);
        await db.StringSetAsync(JobIndexKey(state.JobId), channelKey.ToString(), StateTtl);
    }

    // --- Helpers ---

    private static RedisKey BuildKey(string platform, string channelId) =>
        $"conversation:{platform.ToLowerInvariant()}:{channelId}";

    private static RedisKey JobIndexKey(string jobId) =>
        $"job-index:{jobId}";
}
