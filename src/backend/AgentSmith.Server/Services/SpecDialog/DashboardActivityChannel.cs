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
    /// for a session on any other platform. <paramref name="turn"/> is what the observer
    /// numbers its steps against and keeps them on, for a page arriving mid-turn.
    /// </summary>
    public IDisposable? Observe(ConversationState state, RunningDialogTurn turn) =>
        DialogTarget.IsDashboard(state)
            ? observers.Observe(new DialogActivityObserver(this, DialogTarget.Of(state), turn))
            : null;

    /// <summary>
    /// Sends one step to the dialog. NEVER throws — not even on the caller's own
    /// cancellation, which the reading channel does let through. A step is reported from
    /// inside work that publishes its own outcome afterwards, and a report that threw would
    /// be read there as that work failing: the chat client would publish a second, contrary
    /// LlmCallFinished for a call it had already published as Ok.
    /// </summary>
    public async Task SendAsync(SpecDialogActivityPush step, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(step);
        if (hub is null) return;
        try
        {
            await hub.Clients.Group(HubGroups.SpecDialog(step.DialogId))
                .SendAsync(ActivityMethod, step, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Could not tell spec-dialog {DialogId} that the turn is {Kind}", step.DialogId, step.Kind);
        }
    }
}
