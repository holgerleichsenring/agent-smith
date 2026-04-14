using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Server.Services;

/// <summary>
/// Manages active conversation state in Redis.
/// Maps a chat channel to its currently running K8s job.
/// Delegates Redis I/O to <see cref="ConversationStateRepository"/>.
/// </summary>
public sealed class ConversationStateManager(
    IConnectionMultiplexer redis,
    ILogger<ConversationStateManager> logger,
    ILoggerFactory loggerFactory)
{
    private readonly ConversationStateRepository _repo = new(redis,
        loggerFactory.CreateLogger<ConversationStateRepository>());

    /// <summary>
    /// Persists a new conversation state for the given channel.
    /// Overwrites any existing state (one active job per channel).
    /// </summary>
    public async Task SetAsync(ConversationState state, CancellationToken cancellationToken)
    {
        await _repo.WriteAsync(state);

        logger.LogInformation(
            "Stored conversation state for channel {ChannelId} on {Platform}: job {JobId}",
            state.ChannelId, state.Platform, state.JobId);
    }

    /// <summary>
    /// Retrieves the active conversation state for a channel.
    /// </summary>
    public async Task<ConversationState?> GetAsync(string platform, string channelId,
        CancellationToken cancellationToken) =>
        await _repo.ReadAsync(platform, channelId);

    /// <summary>
    /// Retrieves conversation state by job ID (reverse lookup via index key).
    /// </summary>
    public async Task<ConversationState?> GetByJobIdAsync(string jobId,
        CancellationToken cancellationToken) =>
        await _repo.ReadByJobIndexAsync(jobId);

    /// <summary>
    /// Updates the pending question on an existing conversation state.
    /// </summary>
    public async Task SetPendingQuestionAsync(string platform, string channelId,
        string questionId, CancellationToken cancellationToken)
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
        CancellationToken cancellationToken)
    {
        var existing = await GetAsync(platform, channelId, cancellationToken);
        if (existing is null) return;

        await SetAsync(existing.ClearPendingQuestion(), cancellationToken);
    }

    /// <summary>
    /// Removes all state for a channel after the job completes.
    /// </summary>
    public async Task RemoveAsync(string platform, string channelId,
        CancellationToken cancellationToken)
    {
        var existing = await GetAsync(platform, channelId, cancellationToken);
        await _repo.DeleteAsync(platform, channelId, existing?.JobId);

        logger.LogInformation(
            "Removed conversation state for channel {ChannelId} on {Platform}",
            channelId, platform);
    }

    /// <summary>
    /// Stores a secondary index: jobId -> channel key.
    /// </summary>
    public async Task IndexJobAsync(ConversationState state,
        CancellationToken cancellationToken) =>
        await _repo.IndexJobAsync(state);

    /// <summary>
    /// Updates LastActivityAt on an existing conversation state.
    /// </summary>
    public async Task TouchActivityAsync(string platform, string channelId,
        CancellationToken cancellationToken)
    {
        var existing = await GetAsync(platform, channelId, cancellationToken);
        if (existing is null) return;

        await SetAsync(existing with { LastActivityAt = DateTimeOffset.UtcNow }, cancellationToken);
    }

    /// <summary>
    /// Returns all active conversation states by scanning conversation:* keys.
    /// </summary>
    public async Task<IReadOnlyList<ConversationState>> GetAllAsync(CancellationToken cancellationToken) =>
        await _repo.ScanAllAsync(cancellationToken);
}
