using AgentSmith.Contracts.Dialogue;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Contracts;

/// <summary>
/// Posts into one chat thread on one platform without waiting for anything: a line of text, or
/// a question whose answer comes back later through the platform's interaction endpoint.
/// Unlike <see cref="IPlatformAdapter"/>, every call names the thread, so a run's replies land
/// where it was started rather than at the top of the channel.
/// </summary>
public interface IChatThreadAdapter
{
    string Platform { get; }

    /// <summary>The address beyond channel and thread this platform replies through, as known
    /// now; null when the platform needs none.</summary>
    string? ReplyEndpointFor(string channelId);

    Task PostAsync(ChatThread thread, string text, CancellationToken cancellationToken);

    Task PostQuestionAsync(ChatThread thread, DialogQuestion question, CancellationToken cancellationToken);
}
