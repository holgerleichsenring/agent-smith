namespace AgentSmith.Dispatcher.Models;

/// <summary>
/// Stored in Redis when IntentEngine returns ClarificationNeeded.
/// Cleared after the user clicks a button (confirm or help).
/// </summary>
public sealed record PendingClarification(
    string SuggestedText,
    string OriginalInput,
    string UserId);
