using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-11-12-a1fa: the messages that arrived while a design turn held the gate. A message is queued only while a
/// router design turn holds it on this process, and the router lets go only once nothing waits: what the running turn
/// did not take with a tool result starts the next turn, as the poster of the oldest message waiting.
/// <para>
/// The next turn's prompt renders every transcript turn it has not seen, so every message stored before the state it
/// reads is taken with it, whoever posted it; a message stored after that waits for the turn's tool results.
/// </para>
/// </summary>
public sealed class SpecDialogQueuedTurns(
    SpecDialogOperatorInbox inbox, SpecDialogTurnGate gate, SpecDialogSessionManager sessions,
    SpecDialogAnswerAdmission admission, SpecDialogTurnDelivery delivery, SpecDialogMessenger messenger,
    SpecDialogReplyComposer composer, ILogger<SpecDialogQueuedTurns> logger)
{
    /// <summary>The gate was held: the message is queued for the running design turn, or answered that a turn runs —
    /// or, the gate let go meanwhile, the gate is entered for a turn of its own (true).</summary>
    public async Task<bool> EnterOrWaitAsync(
        SpecDialogAdmitted admitted, TurnPoster poster, string channelId, string threadId, string platform,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(admitted);
        if (await TryQueueAsync(admitted, poster, ct)) return false;
        if (gate.TryEnterTurn(admitted.State.JobId)) return true;
        await admission.UndecideAsync(admitted.State, ct);
        await messenger.SendAsync(platform, channelId, threadId, composer.ComposeTurnInProgress(admitted.State), ct);
        return false;
    }

    // An ordinary message — no answer, no decision on a card — while a design turn holds the gate; an approve or reject
    // word with no card is ordinary and taken back first.
    private async Task<bool> TryQueueAsync(SpecDialogAdmitted admitted, TurnPoster poster, CancellationToken ct)
    {
        var stored = admitted.State.Transcript[^1];
        if (admitted.Decision is not null && stored.DecisionProposalId is not null) return false;
        if (admitted.Decision is not null) await admission.UndecideAsync(admitted.State, ct);
        var index = admitted.State.Transcript.Count - 1;
        return inbox.TryQueue(admitted.State.JobId, new QueuedOperatorMessage(index, stored.Text, stored.At, poster));
    }

    /// <summary>Runs a turn for what waits until nothing does, then releases the gate; true when a turn ran.</summary>
    public async Task<bool> RunThenExitAsync(
        ConversationState state, string channelId, string threadId, string platform, CancellationToken ct)
    {
        var ran = false;
        while (!inbox.TryExit(state.JobId))
        {
            var current = await sessions.GetOpenByThreadAsync(platform, threadId, ct);
            var taken = inbox.TakeBefore(state.JobId, current?.Transcript.Count ?? int.MaxValue);
            if (current is null || taken.Count == 0)
            {
                logger.LogWarning("Dropped {Count} queued message(s) of session {SessionId}: no open state holds them",
                    taken.Count, state.JobId);
                inbox.Drop(state.JobId);
                continue;
            }
            await delivery.RunAsync(current, channelId, threadId, platform, taken[0].Poster, ct);
            ran = true;
        }
        return ran;
    }
}
