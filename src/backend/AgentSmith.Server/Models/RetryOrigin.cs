namespace AgentSmith.Server.Models;

/// <summary>
/// Where a Retry was asked for. 2026-11-17-166ad: both origins retry the same ends — any but a success; the origin
/// only words the answer to a hand-back (the button points at the conversation, the conversation at what to say).
/// </summary>
public enum RetryOrigin
{
    Button,
    Conversation,

    /// <summary>2026-11-22-6d85c: agent-smith's own unchanged retry after the server's stop interrupted the attempt.</summary>
    Restart,
}
