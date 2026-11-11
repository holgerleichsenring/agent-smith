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
    ISpecDialogTurnRunner turnRunner,
    SpecDialogOutcomeFlow outcomeFlow,
    SpecDialogDecisionOutcome decisions, // 2026-11-11-205fe: undecides what it did not decide
    SpecDialogTurnGate turnGate,
    SpecDialogAnswerAdmission admission,
    SpecDialogTurnKeeper keeper,
    SpecDialogReplyComposer composer,
    SpecDialogMessenger messenger,
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

        if (!turnGate.TryEnter(state.JobId))
        {
            await admission.UndecideAsync(state, ct);
            await messenger.SendAsync(platform, channelId, threadId, composer.ComposeTurnInProgress(state), ct);
            return SpecDialogRouted.Handled;
        }
        try
        {
            if (admitted.Decision is { } decision
                && await decisions.DecideAsync(state, decision, null, poster.MayStartRuns, ct, sentAt) is var typed
                && typed != SpecDialogDecided.No)
                return SpecDialogDecisionOutcome.Routed(typed);
            if (admitted.Decision is not null) state = await admission.UndecideAsync(state, ct);
            await RunTurnAsync(state, channelId, threadId, platform, poster, ct);
        }
        finally
        {
            turnGate.Exit(state.JobId);
        }
        // Outside the gate: a message typed while the digest is written starts its own turn.
        await keeper.FoldAsync(state, ct);
        return SpecDialogRouted.Handled;
    }

    private async Task RunTurnAsync(
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
