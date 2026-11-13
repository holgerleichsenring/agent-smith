using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// One design turn, run and answered: the turn is kept with the notice it owes, its reply sent and its outcome handed
/// to the outcome flow. Lifted out of <see cref="SpecDialogRouter"/> by 2026-11-12-a1fa, which runs it for the message
/// that entered the gate and for each message queued while that turn ran.
/// </summary>
public sealed class SpecDialogTurnDelivery(
    ISpecDialogTurnRunner turnRunner, SpecDialogTurnKeeper keeper, SpecDialogMessenger messenger,
    SpecDialogOutcomeFlow outcomeFlow)
{
    public async Task RunAsync(
        ConversationState state, string channelId, string threadId, string platform, TurnPoster poster, CancellationToken ct)
    {
        // 2026-11-10-d854c: a turn that threw has kept and sent its notice with its run id inside the runner.
        if (await turnRunner.RunTurnAsync(state, poster, ct) is not { } result) return;
        if (await keeper.KeepAsync(state, result, platform, threadId, ct) is { } notice) // 2026-11-04-96a0b
            await messenger.SendAsync(platform, channelId, threadId, notice, ct);
        await messenger.SendAsync(platform, channelId, threadId, result.Shown, ct);
        // p0315e: a non-answer outcome is stored as the proposal awaiting a decision and shown; nothing waits.
        await outcomeFlow.HandleAsync(state, result.Outcome, ct, result.Stewarded, result.RunId);
    }
}
