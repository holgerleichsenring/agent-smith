using AgentSmith.Contracts.Dialogue;

namespace AgentSmith.Server.Contracts;

/// <summary>
/// Common contract for all chat platform adapters (Slack, Teams, WhatsApp).
/// Each adapter translates generic dispatcher actions into platform-specific API calls.
/// </summary>
public interface IPlatformAdapter
{
    /// <summary>
    /// The platform identifier this adapter handles (e.g. "slack", "teams").
    /// </summary>
    string Platform { get; }

    /// <summary>
    /// Sends a plain text message to the specified channel.
    /// </summary>
    Task SendMessageAsync(string channelId, string text,
        CancellationToken cancellationToken);

    /// <summary>
    /// Asks a typed question. Blocks until answer or timeout.
    /// Returns null on timeout (agent uses DefaultAnswer).
    /// When threadId is set, the question is posted as a reply in that thread
    /// (Slack: thread_ts; Teams: the conversation id is already thread-scoped).
    /// </summary>
    Task<DialogAnswer?> AskTypedQuestionAsync(
        string channelId,
        DialogQuestion question,
        string? threadId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Sends an informational message with acknowledge indication (no waiting).
    /// When threadId is set, the message is posted as a reply in that thread
    /// (Slack: thread_ts; Teams: the conversation id is already thread-scoped).
    /// </summary>
    Task SendInfoAsync(string channelId, string title, string text,
        string? threadId, CancellationToken cancellationToken);

    /// <summary>
    /// Updates or replaces the interactive question message after the user has answered.
    /// Used to remove the buttons and show the selected answer instead.
    /// </summary>
    Task UpdateQuestionAnsweredAsync(string channelId, string messageId, string questionText,
        string answer, CancellationToken cancellationToken);

    /// <summary>
    /// Sends a clarification question with confirm/help buttons.
    /// Used when the IntentEngine has low confidence and needs user confirmation.
    /// </summary>
    Task SendClarificationAsync(string channelId, string suggestion,
        CancellationToken cancellationToken);
}
