using AgentSmith.Contracts.Dialogue;
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

    /// <summary>
    /// p0315c: posts a typed question threaded via the platform's generic
    /// question blocks/cards (Slack Block Kit approve/reject buttons, Teams
    /// Adaptive Card actions — the p0058 surface) and waits for the button
    /// answer. Returns null on timeout, cancellation, or when the platform
    /// has no adapter — text replies in the thread stay the fallback path.
    /// </summary>
    public async Task<DialogAnswer?> AskQuestionAsync(
        string platform, string channelId, string threadId, DialogQuestion question,
        CancellationToken cancellationToken)
    {
        if (!_adapters.TryGetValue(platform, out var adapter))
        {
            logger.LogWarning(
                "No platform adapter for '{Platform}' — spec-dialog question buttons unavailable, "
                + "text replies in the thread still work", platform);
            return null;
        }
        return await adapter.AskTypedQuestionAsync(channelId, question, threadId, cancellationToken);
    }
}
