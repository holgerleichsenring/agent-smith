using AgentSmith.Dispatcher.Models;

namespace AgentSmith.Dispatcher.Contracts;

/// <summary>
/// Classifies free-form user input into a typed ChatIntent using an LLM.
/// Returns null if the parser is unavailable (no API key) or cannot classify.
/// </summary>
public interface ILlmIntentParser
{
    Task<ChatIntent?> ParseAsync(
        string text, string userId, string channelId, string platform,
        CancellationToken cancellationToken);
}
