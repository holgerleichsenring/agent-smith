using AgentSmith.Dispatcher.Models;

namespace AgentSmith.Dispatcher.Services;

/// <summary>
/// Classifies free-form user input into a typed ChatIntent using Claude Haiku.
/// Returns null if the parser is unavailable (no API key) or cannot classify.
/// </summary>
public interface IHaikuIntentParser
{
    Task<ChatIntent?> ParseAsync(
        string text, string userId, string channelId, string platform,
        CancellationToken cancellationToken = default);
}
