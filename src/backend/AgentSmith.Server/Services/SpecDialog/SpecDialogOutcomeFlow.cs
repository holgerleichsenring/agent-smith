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
    ILogger<SpecDialogOutcomeFlow> logger)
{
    public async Task<OutcomeFlowResult> HandleAsync(
        ConversationState state, OutcomeProposal proposal, CancellationToken cancellationToken)
    {
        if (proposal is AnswerOutcome) return new OutcomeFlowCompleted();

        var confirmation = await confirmer.ConfirmAsync(state, proposal, cancellationToken);
        logger.LogInformation(
            "Outcome {Kind} for spec-dialog session {SessionId}: {Confirmation}",
            proposal.GetType().Name, state.JobId, confirmation.GetType().Name);

        switch (confirmation)
        {
            case OutcomeConfirmed:
                await outcomeSink.AcceptAsync(state, proposal, cancellationToken);
                return new OutcomeFlowCompleted();
            case OutcomeEditRequested edit:
                await SendAsync(state, composer.ComposeEditAck(edit.Note), cancellationToken);
                return new OutcomeFlowEditRequested(edit.Note);
            case OutcomeRejected:
                await SendAsync(state, composer.ComposeRejected(), cancellationToken);
                return new OutcomeFlowCompleted();
            default:
                await SendAsync(state, composer.ComposeTimeout(), cancellationToken);
                return new OutcomeFlowCompleted();
        }
    }

    private Task SendAsync(ConversationState state, string text, CancellationToken ct) =>
        messenger.SendAsync(state.Platform, state.ChannelId, state.ThreadId!, text, ct);
}
