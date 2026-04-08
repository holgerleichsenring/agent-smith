namespace AgentSmith.Contracts.Services;

/// <summary>
/// Looks up active conversation state to find the job associated with a PR channel.
/// Used by WebhookListener to route dialogue answers to the correct agent job.
/// </summary>
public interface IConversationLookup
{
    /// <summary>
    /// Finds the active job ID and pending question ID for a PR-based channel.
    /// Channel format: pr:{repoFullName}#{prIdentifier}
    /// Returns null if no active job exists for this channel.
    /// </summary>
    Task<ConversationLookupResult?> FindByPrAsync(
        string platform, string repoFullName, string prIdentifier,
        CancellationToken cancellationToken);
}

/// <summary>
/// Result of a conversation lookup for dialogue routing.
/// </summary>
public sealed record ConversationLookupResult(
    string JobId,
    string ChannelId,
    string? PendingQuestionId);
