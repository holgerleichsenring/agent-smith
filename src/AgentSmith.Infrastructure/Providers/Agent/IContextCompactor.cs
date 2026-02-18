using Anthropic.SDK.Messaging;

namespace AgentSmith.Infrastructure.Providers.Agent;

/// <summary>
/// Compacts conversation history by summarizing old messages.
/// Keeps recent messages intact while replacing older ones with a summary.
/// Internal to the Claude agent provider infrastructure.
/// </summary>
public interface IContextCompactor
{
    Task<List<Message>> CompactAsync(
        List<Message> messages,
        int keepRecentMessages,
        CancellationToken cancellationToken = default);
}
