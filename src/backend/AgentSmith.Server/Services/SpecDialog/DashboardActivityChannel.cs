using AgentSmith.Contracts.Turns;
using AgentSmith.Server.Hubs;
using AgentSmith.Server.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// Tells the dashboard what a design turn is DOING between the repositories it opens and the
/// answer it returns — each tool it calls, each model call that returns, the review of its own
/// proposal and each re-prompt that sends it back to revise.
/// <para>
/// The dashboard platform only, as the reading channel is: a chat platform shows its own
/// delivery and expects the answer later, and its thread id is a dialog id nobody has joined.
/// </para>
/// <para>
/// Into the dialog's own group, whose membership is owner-checked, and never into the run
/// group: a design turn IS a run, and the run subscription checks nobody.
/// </para>
/// </summary>
public sealed class DashboardActivityChannel(
    ITurnActivityObserverAccessor observers,
    ILogger<DashboardActivityChannel> logger,
    IHubContext<JobsHub>? hub = null)
{
    private const string ActivityMethod = "SpecDialogActivity";

    /// <summary>
    /// Sets the turn's activity observer until the handle is disposed; null — nothing set —
    /// for a session on any other platform.
    /// </summary>
    public IDisposable? Observe(ConversationState state) =>
        DialogTarget.IsDashboard(state)
            ? observers.Observe(new DialogActivityObserver(this, DialogTarget.Of(state)))
            : null;

    /// <summary>
    /// Pushes one step to the dialog. NEVER throws — not even on the caller's own
    /// cancellation, which the reading channel does let through. A step is reported from
    /// inside work that publishes its own outcome afterwards, and a report that threw would
    /// be read there as that work failing: the chat client would publish a second, contrary
    /// LlmCallFinished for a call it had already published as Ok.
    /// </summary>
    public async Task PushAsync(string dialogId, TurnActivity activity, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(activity);
        if (hub is null) return;
        try
        {
            var push = new SpecDialogActivityPush(
                dialogId, activity.Kind.ToString().ToLowerInvariant(),
                activity.Name, activity.Detail, DateTimeOffset.UtcNow);
            await hub.Clients.Group(HubGroups.SpecDialog(dialogId)).SendAsync(ActivityMethod, push, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Could not tell spec-dialog {DialogId} that the turn is {Kind}", dialogId, activity.Kind);
        }
    }
}
