using AgentSmith.Contracts.Sandbox;
using AgentSmith.Server.Hubs;
using AgentSmith.Server.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// Tells the dashboard which repositories a design turn opens and how each fared, so the
/// minute a turn takes names what the agent is reading instead of showing a spinner.
/// <para>
/// The dashboard platform only. A chat platform shows its own delivery and expects the answer
/// later, and its thread id is a dialog id nobody has joined.
/// </para>
/// <para>
/// The dialog is taken from the turn's state, as the outcome channel takes it: a resume is
/// refused while a turn runs, so the conversation cannot move to another dialog mid-turn.
/// </para>
/// </summary>
public sealed class DashboardReadingChannel(
    ISourceScopeObserverAccessor observers,
    ILogger<DashboardReadingChannel> logger,
    IHubContext<JobsHub>? hub = null)
{
    private const string ReadingMethod = "SpecDialogReading";

    /// <summary>
    /// Sets the turn's observer until the handle is disposed; null — nothing set — for a
    /// session on any other platform.
    /// </summary>
    public IDisposable? Observe(ConversationState state) =>
        DialogTarget.IsDashboard(state)
            ? observers.Observe(new DialogReadingObserver(this, DialogTarget.Of(state)))
            : null;

    /// <summary>
    /// Pushes one report to the dialog. Never throws unless the caller cancelled: a progress
    /// line that failed would fail the read it announces.
    /// </summary>
    public async Task PushAsync(
        string dialogId, string repoName, SourceScopeProgress progress, CancellationToken ct)
    {
        if (hub is null) return;
        try
        {
            var push = new SpecDialogReadingPush(
                dialogId, repoName, progress.ToString().ToLowerInvariant(), DateTimeOffset.UtcNow);
            await hub.Clients.Group(HubGroups.SpecDialog(dialogId))
                .SendAsync(ReadingMethod, push, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex,
                "Could not tell spec-dialog {DialogId} that '{Repo}' is {Progress}",
                dialogId, repoName, progress);
        }
    }
}
