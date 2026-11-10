using AgentSmith.Contracts.Turns;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// The activity observer one dashboard design turn sets: every step goes to the dialog it
/// runs on.
/// <para>
/// 2026-09-18-2f8b: and is KEPT on the turn, under a sequence allocated here and stamped with
/// the turn it belongs to. This is the one site that knows both the turn and the dialog — the
/// channel below it is handed a payload and nothing else — so the pair a page merges by is
/// minted here or nowhere.
/// </para>
/// <para>2026-11-10-d854c: and kept on the turn's run by <paramref name="recorder"/>, so every step outlives the turn.</para>
/// </summary>
public sealed class DialogActivityObserver(
    DashboardActivityChannel channel, string dialogId, RunningDialogTurn turn, DialogActivityRecorder? recorder = null)
    : ITurnActivityObserver
{
    public async Task ReportAsync(TurnActivity activity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(activity);
        var step = new SpecDialogActivityPush(
            dialogId, activity.Kind.ToString().ToLowerInvariant(), activity.Name, activity.Detail,
            turn.Now, turn.Next(), turn.StartedAt);
        turn.Keep(step);
        if (recorder is not null) await recorder.RecordAsync(step, cancellationToken);
        await channel.SendAsync(step, cancellationToken);
    }
}
