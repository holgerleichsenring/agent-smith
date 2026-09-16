using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-15-9033: one message from the dashboard channel, routed. The spec-dialog
/// router answers a /spec command and every message inside an open session; anything else
/// it hands back for the intent path, and a dedicated dialog page has no other
/// conversation to fall through to — so an unroutable message is ANSWERED here rather
/// than dropped the way a chat-channel aside is.
/// </summary>
public sealed class DashboardDialogDispatcher(
    SpecDialogRouter router,
    SpecDialogMessenger messenger,
    ILogger<DashboardDialogDispatcher> logger)
{
    private const string Platform = DispatcherDefaults.PlatformDashboard;

    private const string NoOpenDialog =
        "No spec dialog is open here. Start one with `/spec` — or `/spec <project>` when "
        + "several projects are configured; `/spec list` shows the sessions you can resume.";

    public async Task DispatchAsync(
        string dialogId, string text, string userId, CancellationToken cancellationToken)
    {
        try
        {
            // The dialog id is both channel and thread: a browser page holds exactly one
            // conversation and has no channel above it to group them by.
            if (await router.TryRouteAsync(
                    text, userId, dialogId, dialogId, Platform, cancellationToken))
                return;
            await TellAsync(dialogId, NoOpenDialog, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "The spec dialog {DialogId} could not continue", dialogId);
            await TellAsync(dialogId, $"The spec dialog could not continue: {ex.Message}",
                CancellationToken.None);
        }
    }

    // The page is the only place this conversation exists, so failing to reach it leaves
    // a person waiting on a reply that will never arrive — worth a warning of its own.
    private async Task TellAsync(string dialogId, string text, CancellationToken cancellationToken)
    {
        try
        {
            await messenger.SendAsync(Platform, dialogId, dialogId, text, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "The spec dialog {DialogId} could not be told what happened", dialogId);
        }
    }
}
