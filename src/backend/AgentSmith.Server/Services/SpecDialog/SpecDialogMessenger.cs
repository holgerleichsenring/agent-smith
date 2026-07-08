using AgentSmith.Server.Contracts;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// Sends spec-dialog replies threaded via the matching platform adapter's
/// SendInfoAsync (Slack: thread_ts, Teams: thread-scoped conversation id).
/// </summary>
public sealed class SpecDialogMessenger(
    IEnumerable<IPlatformAdapter> adapters,
    ILogger<SpecDialogMessenger> logger)
{
    private const string ReplyTitle = "Spec dialog";

    private readonly Dictionary<string, IPlatformAdapter> _adapters =
        adapters.ToDictionary(a => a.Platform, StringComparer.OrdinalIgnoreCase);

    public async Task SendAsync(
        string platform, string channelId, string threadId, string text,
        CancellationToken cancellationToken)
    {
        if (!_adapters.TryGetValue(platform, out var adapter))
        {
            logger.LogWarning(
                "No platform adapter for '{Platform}' — spec-dialog reply dropped", platform);
            return;
        }
        await adapter.SendInfoAsync(channelId, ReplyTitle, text, threadId, cancellationToken);
    }
}
