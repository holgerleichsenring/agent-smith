using AgentSmith.Contracts.Turns;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// The activity observer one dashboard design turn sets: every step goes to the dialog it
/// runs on.
/// </summary>
public sealed class DialogActivityObserver(DashboardActivityChannel channel, string dialogId)
    : ITurnActivityObserver
{
    public Task ReportAsync(TurnActivity activity, CancellationToken cancellationToken) =>
        channel.PushAsync(dialogId, activity, cancellationToken);
}
