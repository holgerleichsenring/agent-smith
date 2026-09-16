using AgentSmith.Server.Contracts;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Adapters;

/// <summary>
/// 2026-09-15-9033: the adapters by platform, for callers that learn their platform at
/// call time rather than at construction.
/// <para>
/// A single non-keyed <see cref="IPlatformAdapter"/> resolve yields whichever was
/// registered last, so a class serving more than one channel cannot take one by injection
/// — it would reply to every channel through the same one. The dispatcher below serves
/// Slack, Teams and the dashboard from one instance and is handed the platform on every
/// call; this is how it answers on the channel the message came from.
/// </para>
/// </summary>
public sealed class PlatformAdapters(
    IEnumerable<IPlatformAdapter> adapters, ILogger<PlatformAdapters> logger)
{
    private readonly Dictionary<string, IPlatformAdapter> _byPlatform =
        adapters.ToDictionary(adapter => adapter.Platform, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Sends on the named platform, or says so. A reply nobody can deliver is logged
    /// rather than thrown: it is already the error path, and a throw here would replace a
    /// message the operator cannot see with a stack trace they also cannot see.
    /// </summary>
    public async Task SendMessageAsync(
        string platform, string channelId, string text, CancellationToken cancellationToken)
    {
        if (!_byPlatform.TryGetValue(platform, out var adapter))
        {
            logger.LogWarning(
                "No platform adapter for '{Platform}' — the message was not delivered: {Text}",
                platform, text);
            return;
        }
        await adapter.SendMessageAsync(channelId, text, cancellationToken);
    }
}
