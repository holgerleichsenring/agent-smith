using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// Routes a turn's typed terminal outcome. Answer → nothing (today's default
/// path). Bug / phase / epic → shown in-thread and CONFIRMED at the approval
/// gate first; only a confirmed proposal reaches the outcome sink (p0315c:
/// real ticket filing). Rejected / timed-out proposals route nowhere and say
/// so; an edit note is handed back to the router, which re-runs the design
/// turn over the transcript that now ends with the note.
/// </summary>
public sealed class SpecDialogOutcomeFlow(
    SpecDialogOutcomeConfirmer confirmer,
    IOutcomeSink outcomeSink,
    SpecDialogOutcomeComposer composer,
    SpecDialogMessenger messenger,
    DashboardOutcomeChannel outcomeChannel,
    SpecDialogLatestOutcomeStore latestOutcome,
    ILogger<SpecDialogOutcomeFlow> logger)
{
    public async Task<OutcomeFlowResult> HandleAsync(
        ConversationState state, OutcomeProposal proposal, bool mayStartRuns,
        CancellationToken cancellationToken)
    {
        // The pane shows what would be filed BEFORE the approval is asked, so the person
        // holding the question reads the proposal itself rather than a summary of it. An
        // answer proposes nothing and publishes nothing — the channel decides that, because
        // "which outcomes have a shape" is the same question the pane asks.
        await outcomeChannel.ProposeAsync(state, proposal, cancellationToken);
        if (proposal is AnswerOutcome) return new OutcomeFlowCompleted();
        // Kept beside the push, never for an answer: an answer leaves the proposal under
        // discussion where it was, live and after a reload alike.
        await latestOutcome.SetProposalAsync(state.Platform, state.ThreadId!, proposal, cancellationToken);

        var confirmation = await confirmer.ConfirmAsync(state, proposal, cancellationToken);
        logger.LogInformation(
            "Outcome {Kind} for spec-dialog session {SessionId}: {Confirmation}",
            proposal.GetType().Name, state.JobId, confirmation.GetType().Name);

        return await ApplyAsync(state, proposal, confirmation, mayStartRuns, cancellationToken);
    }

    /// <summary>
    /// What a confirmation does. 2026-09-22-355b: the timeout is its OWN case and the default
    /// throws. It used to fall through to the timeout, so a result added later would have
    /// cleared the stored proposal and told the thread nothing would be filed — silently
    /// destroying it instead of failing to compile.
    /// </summary>
    internal async Task<OutcomeFlowResult> ApplyAsync(
        ConversationState state, OutcomeProposal proposal, ConfirmationResult confirmation,
        bool mayStartRuns, CancellationToken cancellationToken)
    {
        switch (confirmation)
        {
            case OutcomeConfirmed:
                await outcomeSink.AcceptAsync(state, proposal, mayStartRuns, cancellationToken);
                return new OutcomeFlowCompleted();
            // 2026-09-25-8e51e: an approval of a different act. The stored proposal is cleared
            // like an approval's, because it has been acted on — the pane must not offer it again.
            case OutcomeAmendRequested:
                await outcomeSink.AmendAsync(state, proposal, cancellationToken);
                await latestOutcome.ClearProposalAsync(state.Platform, state.ThreadId!, cancellationToken);
                return new OutcomeFlowCompleted();
            case OutcomeEditRequested edit:
                await SendAsync(state, composer.ComposeEditAck(edit.Note), cancellationToken);
                return new OutcomeFlowEditRequested(edit.Note);
            case OutcomeRejected:
                await latestOutcome.ClearProposalAsync(state.Platform, state.ThreadId!, cancellationToken);
                await SendAsync(state, composer.ComposeRejected(), cancellationToken);
                return new OutcomeFlowCompleted();
            case OutcomeConfirmationTimedOut:
                // A timed-out approval is over as surely as a rejected one; the pane must not
                // offer it after a reload. An edit note keeps it, because it is being revised.
                await latestOutcome.ClearProposalAsync(state.Platform, state.ThreadId!, cancellationToken);
                await SendAsync(state, composer.ComposeTimeout(), cancellationToken);
                return new OutcomeFlowCompleted();
            default:
                throw new InvalidOperationException(
                    $"Confirmation result '{confirmation.GetType().Name}' has no outcome flow — "
                    + "a new result must say what it does with the stored proposal.");
        }
    }

    private Task SendAsync(ConversationState state, ComposedReply notice, CancellationToken ct) =>
        messenger.SendAsync(state.Platform, state.ChannelId, state.ThreadId!, notice, ct);
}
