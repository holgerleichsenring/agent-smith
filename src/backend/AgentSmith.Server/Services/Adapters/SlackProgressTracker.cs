using System.Collections.Concurrent;

namespace AgentSmith.Server.Services.Adapters;

/// <summary>
/// Tracks Slack progress message timestamps per channel, enabling
/// in-place updates instead of flooding new messages.
/// </summary>
internal sealed class SlackProgressTracker
{
    private readonly ConcurrentDictionary<string, string> _messageTs = new();

    internal string? GetThreadTs(string channelId) =>
        _messageTs.TryGetValue(channelId, out var ts) ? ts : null;

    internal void SetThreadTs(string channelId, string ts) =>
        _messageTs[channelId] = ts;

    internal void Remove(string channelId) =>
        _messageTs.TryRemove(channelId, out _);
}
