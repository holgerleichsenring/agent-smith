using AgentSmith.Dispatcher.Models;

namespace AgentSmith.Dispatcher.Adapters;

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
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a progress update to the channel.
    /// Implementations may update an existing message instead of posting a new one.
    /// </summary>
    Task SendProgressAsync(string channelId, int step, int total, string commandName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks a yes/no question in the channel with interactive buttons.
    /// Returns the message ID so it can be updated after the user responds.
    /// </summary>
    Task<string> AskQuestionAsync(string channelId, string questionId, string text,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a completion message with a link to the created pull request.
    /// </summary>
    Task SendDoneAsync(string channelId, string summary, string? prUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a structured error message with optional action buttons.
    /// </summary>
    Task SendErrorAsync(string channelId, ErrorContext errorContext,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates or replaces the interactive question message after the user has answered.
    /// Used to remove the buttons and show the selected answer instead.
    /// </summary>
    Task UpdateQuestionAnsweredAsync(string channelId, string messageId, string questionText,
        string answer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a low-level detail message (e.g. tool calls, iteration progress).
    /// Implementations may post as thread replies under the progress message.
    /// </summary>
    Task SendDetailAsync(string channelId, string text,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a clarification question with confirm/help buttons.
    /// Used when the IntentEngine has low confidence and needs user confirmation.
    /// </summary>
    Task SendClarificationAsync(string channelId, string suggestion,
        CancellationToken cancellationToken = default);
}
