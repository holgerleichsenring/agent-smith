using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>What routing did with a message: not the spec dialog's, handled, or a decision taken on a card.</summary>
public enum SpecDialogRouted
{
    No,
    Handled,
    Decided,
}

/// <summary>
/// The SpecDialog branch of inbound chat routing: /spec commands and follow-up
/// messages inside a thread with an open spec-dialog session are handled here;
/// everything else returns false so normal chat + run-triggers stay untouched.
/// p0315b: a follow-up turn runs the design-partner master (one in-process
/// spec-dialog pipeline run) instead of the p0315a receipt; a turn blocked on
/// an ask_human question consumes the next thread message as its answer.
/// <para>2026-11-10-ccd9: an approve or reject decides a card and runs no turn; any other message, a clicked shape
/// included, is an ordinary message that runs an ordinary turn. A decision that names its card by id is one
/// compare-and-set and needs no turn gate, so a busy turn never swallows it; a typed word is decided inside the gate.</para>
/// </summary>
public sealed class SpecDialogRouter(
    SpecCommandParser parser,
    SpecDialogCommandHandler commandHandler,
    SpecDialogTurnDelivery delivery,
    SpecDialogQueuedTurns queued, // 2026-11-12-a1fa: a message sent while a design turn runs reaches it
    SpecDialogDecisionOutcome decisions, // 2026-11-11-205fe: undecides what it did not decide
    SpecDialogTurnGate turnGate,
    SpecDialogOperatorInbox inbox,
    SpecDialogAnswerAdmission admission,
    SpecDialogTurnKeeper keeper,
    ILogger<SpecDialogRouter> logger)
{
    /// <summary>
    /// Routes the message if it belongs to the spec-dialog flow. Returns true when handled; false hands
    /// the message back to the normal intent path. The poster's display name is recorded on its turn.
    /// <paramref name="proposalId"/> is the card the message was sent from, when a surface names one;
    /// <paramref name="sentAt"/> the platform's own time of a typed chat message.
    /// </summary>
    public async Task<bool> TryRouteAsync(
        string text, string userId, string channelId, string? threadId,
        string platform, bool mayStartRuns, CancellationToken ct, string? author = null, string? proposalId = null,
        DateTimeOffset? sentAt = null) =>
        await RouteAsync(text, userId, channelId, threadId, platform, mayStartRuns, ct, author, proposalId, sentAt)
            != SpecDialogRouted.No;

    /// <summary>The same routing, saying whether a card was decided — what a clicked button may mark answered.</summary>
    public async Task<SpecDialogRouted> RouteAsync(
        string text, string userId, string channelId, string? threadId, string platform, bool mayStartRuns,
        CancellationToken ct, string? author = null, string? proposalId = null, DateTimeOffset? sentAt = null)
    {
        if (parser.Parse(text) is not { } command)
            return await ContinueThreadAsync(text, userId, channelId, threadId, platform,
                new TurnPoster(userId, mayStartRuns), author, proposalId, sentAt, ct);
        if (threadId is null)
        {
            logger.LogWarning("/spec received without a thread context on {Platform}, ignoring", platform);
            return SpecDialogRouted.No;
        }
        await commandHandler.HandleAsync(command, userId, channelId, threadId, platform, ct);
        return SpecDialogRouted.Handled;
    }

    private async Task<SpecDialogRouted> ContinueThreadAsync(
        string text, string userId, string channelId, string? threadId, string platform, TurnPoster poster,
        string? author, string? proposalId, DateTimeOffset? sentAt, CancellationToken ct)
    {
        if (threadId is null) return SpecDialogRouted.No;
        // Stored first, whatever happens next: a live question takes it as the answer, a busy gate
        // still keeps it in the transcript.
        var admitted = await admission.AdmitAsync(text, userId, platform, threadId, ct, author, proposalId);
        if (admitted is null) return SpecDialogRouted.No;
        if (admitted.Answered) return SpecDialogRouted.Handled;
        var state = admitted.State;
        if (admitted.Decision is { } named && proposalId is not null)
            return SpecDialogDecisionOutcome.Routed(await decisions.DecideAsync(state, named, proposalId, poster.MayStartRuns, ct));

        // 2026-11-12-a1fa: a held gate queues the message for the running design turn or answers that a turn runs.
        if (!turnGate.TryEnterTurn(state.JobId)
            && !await queued.EnterOrWaitAsync(admitted, poster, channelId, threadId, platform, ct))
            return SpecDialogRouted.Handled;
        var exited = false;
        try
        {
            var routed = await DecideOrRunAsync(admitted, state, channelId, threadId, platform, poster, sentAt, ct);
            var ranQueued = await queued.RunThenExitAsync(state, channelId, threadId, platform, ct);
            exited = true;
            // Outside the gate: a message typed while the digest is written starts its own turn.
            if (routed is null || ranQueued) await keeper.FoldAsync(state, ct);
            return routed ?? SpecDialogRouted.Handled;
        }
        finally
        {
            if (!exited) inbox.Abandon(state.JobId); // a thrown turn: what waited stays in the transcript
        }
    }

    /// <summary>A typed decision decided inside the gate, or the turn the message runs (null).</summary>
    private async Task<SpecDialogRouted?> DecideOrRunAsync(
        SpecDialogAdmitted admitted, ConversationState state, string channelId, string threadId, string platform,
        TurnPoster poster, DateTimeOffset? sentAt, CancellationToken ct)
    {
        if (admitted.Decision is { } decision
            && await decisions.DecideAsync(state, decision, null, poster.MayStartRuns, ct, sentAt) is var typed
            && typed != SpecDialogDecided.No)
            return SpecDialogDecisionOutcome.Routed(typed);
        if (admitted.Decision is not null) state = await admission.UndecideAsync(state, ct);
        await delivery.RunAsync(state, channelId, threadId, platform, poster, ct);
        return null;
    }
}
