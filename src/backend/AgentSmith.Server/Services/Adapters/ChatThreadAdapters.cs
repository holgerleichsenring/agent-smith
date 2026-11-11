using AgentSmith.Contracts.Dialogue;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Adapters;

/// <summary>
/// The thread posters by platform. A chat run is answered on the platform its thread is on —
/// the platform the binding recorded, never whichever adapter a single resolve happens to yield.
/// A thread on a platform this server has no poster for is logged, not thrown: the run goes on
/// either way, and the dashboard still shows it.
/// </summary>
public sealed class ChatThreadAdapters(
    IEnumerable<IChatThreadAdapter> adapters, ILogger<ChatThreadAdapters> logger)
{
    public string? ReplyEndpointFor(string platform, string channelId) =>
        Find(platform)?.ReplyEndpointFor(channelId);

    public async Task PostAsync(ChatThread thread, string text, CancellationToken cancellationToken)
    {
        if (Resolve(thread) is { } adapter) await adapter.PostAsync(thread, text, cancellationToken);
    }

    public async Task PostQuestionAsync(ChatThread thread, DialogQuestion question, CancellationToken cancellationToken) =>
        await TryPostQuestionAsync(thread, question, cancellationToken);

    /// <summary>2026-11-10-ccd9: the same post, saying what was posted — null when no poster took it, a card nobody posted
    /// is not one a thread was shown. 2026-11-11-205fh: with the platform's own id and time.</summary>
    public async Task<PostedMessage?> TryPostQuestionAsync(ChatThread thread, DialogQuestion question, CancellationToken cancellationToken) =>
        Resolve(thread) is { } adapter ? await adapter.PostQuestionAsync(thread, question, cancellationToken) : null;

    private IChatThreadAdapter? Resolve(ChatThread thread)
    {
        var adapter = Find(thread.Platform);
        if (adapter is null)
            logger.LogWarning(
                "No chat thread adapter for '{Platform}' — nothing posted to {Channel}",
                thread.Platform, thread.ChannelId);
        return adapter;
    }

    private IChatThreadAdapter? Find(string platform) =>
        adapters.FirstOrDefault(a => string.Equals(a.Platform, platform, StringComparison.OrdinalIgnoreCase));
}
