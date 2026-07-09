using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// p0315e: routes a turn's typed terminal outcome. Answer → nothing (today's
/// default path). Bug / phase / epic → shown in-thread and CONFIRMED at the
/// approval gate first; only a confirmed proposal reaches the outcome sink
/// (the p0315c filing seam). Declined or timed-out proposals route nowhere.
/// </summary>
public sealed class SpecDialogOutcomeFlow(
    SpecDialogOutcomeConfirmer confirmer,
    IOutcomeSink outcomeSink,
    SpecDialogOutcomeComposer composer,
    SpecDialogMessenger messenger,
    ILogger<SpecDialogOutcomeFlow> logger)
{
    public async Task HandleAsync(
        ConversationState state, OutcomeProposal proposal, CancellationToken cancellationToken)
    {
        if (proposal is AnswerOutcome) return;

        var confirmation = await confirmer.ConfirmAsync(state, proposal, cancellationToken);
        logger.LogInformation(
            "Outcome {Kind} for spec-dialog session {SessionId}: {Confirmation}",
            proposal.GetType().Name, state.JobId, confirmation);

        if (confirmation == ConfirmationResult.Confirmed)
        {
            await outcomeSink.AcceptAsync(state, proposal, cancellationToken);
            return;
        }
        var notice = confirmation == ConfirmationResult.TimedOut
            ? composer.ComposeTimeout()
            : composer.ComposeDeclined();
        await messenger.SendAsync(
            state.Platform, state.ChannelId, state.ThreadId!, notice, cancellationToken);
    }
}
