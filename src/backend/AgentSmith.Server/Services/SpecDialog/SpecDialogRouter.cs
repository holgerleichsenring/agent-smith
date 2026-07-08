using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// The SpecDialog branch of inbound chat routing: /spec commands and follow-up
/// messages inside a thread with an open spec-dialog session are handled here;
/// everything else returns false so normal chat + run-triggers stay untouched.
/// </summary>
public sealed class SpecDialogRouter(
    SpecCommandParser parser,
    SpecDialogSessionManager sessions,
    SpecDialogCommandHandler commandHandler,
    SpecDialogReplyComposer composer,
    SpecDialogMessenger messenger,
    ILogger<SpecDialogRouter> logger)
{
    /// <summary>
    /// Routes the message if it belongs to the spec-dialog flow. Returns true
    /// when handled; false hands the message back to the normal intent path.
    /// </summary>
    public async Task<bool> TryRouteAsync(
        string text, string userId, string channelId, string? threadId,
        string platform, CancellationToken ct)
    {
        var command = parser.Parse(text);
        if (command is null)
            return await TryContinueThreadAsync(text, channelId, threadId, platform, ct);

        if (threadId is null)
        {
            logger.LogWarning("/spec received without a thread context on {Platform}, ignoring", platform);
            return false;
        }

        await commandHandler.HandleAsync(command, userId, channelId, threadId, platform, ct);
        return true;
    }

    private async Task<bool> TryContinueThreadAsync(
        string text, string channelId, string? threadId, string platform, CancellationToken ct)
    {
        if (threadId is null) return false;

        var state = await sessions.AppendTurnAsync(platform, threadId, TranscriptRole.User, text, ct);
        if (state is null) return false;

        await messenger.SendAsync(platform, channelId, threadId, composer.ComposeTurnRecorded(state), ct);
        return true;
    }
}
