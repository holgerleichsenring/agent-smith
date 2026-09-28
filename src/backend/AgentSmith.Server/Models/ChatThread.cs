using AgentSmith.Contracts.Models;

namespace AgentSmith.Server.Models;

/// <summary>
/// Where a chat conversation happens and who spoke in it: the platform the message came from,
/// its channel and thread, the person, and — for a platform that needs one — the address the
/// reply is posted through. A run started there reports back to exactly this place.
/// </summary>
public sealed record ChatThread(
    string Platform,
    string ChannelId,
    string? ThreadId,
    string RequestedBy,
    string? ReplyEndpoint = null)
{
    public static ChatThread Of(ChatIntent intent) =>
        new(intent.Platform, intent.ChannelId, intent.ThreadId, intent.UserId);

    public static ChatThread Of(ChatRunBindingFact binding) =>
        new(binding.Platform, binding.ChannelId, binding.ThreadId, binding.RequestedBy, binding.ReplyEndpoint);
}
