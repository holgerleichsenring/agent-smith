using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// Puts a turn's typed terminal outcome before the person. An answer proposes nothing. A bug, spec,
/// epic, list or comment is stored as the proposal awaiting a decision and then shown — on the page, and
/// as a card in a chat thread.
/// <para>
/// 2026-11-10-ccd9: nothing waits and nothing expires. The proposal is stored BEFORE it is shown, so no
/// card exists that the row does not hold; a decision on it arrives later as a message carrying its id
/// and goes through <see cref="SpecDialogDecisionPath"/>. A proposal that replaces an awaiting one says so.
/// </para>
/// <para>
/// 2026-11-12-92e9c: only in a chat thread, whose card stays postable. On the dashboard a shown turn never changes, so
/// a notice declaring an earlier turn void would be the one line that rewrites history; the composer shows what awaits.
/// </para>
/// </summary>
public sealed class SpecDialogOutcomeFlow(
    SpecDialogProposalState proposals,
    SpecDialogOutcomeComposer composer,
    SpecDialogReplyComposer replies,
    SpecDialogNotices notices,
    DashboardOutcomeChannel outcomeChannel,
    SpecDialogChatCard chatCards,
    ILogger<SpecDialogOutcomeFlow> logger,
    SpecDialogTurnProposalStore? turnProposals = null) // 2026-11-10-d854c
{
    /// <summary>
    /// <paramref name="stewarded"/>: the ticket steward led the turn. A proposal awaiting before it took over is
    /// cleared, so the series changes through edit_series alone and no stale card can be approved beside it.
    /// <paramref name="runId"/> keys the turn's own proposal row (2026-11-10-d854c).
    /// </summary>
    public async Task HandleAsync(
        ConversationState state, OutcomeProposal proposal, CancellationToken cancellationToken, bool stewarded = false,
        string? runId = null)
    {
        if (stewarded && await proposals.SupersedeAsync(state, cancellationToken) is not null)
        {
            logger.LogInformation("The ticket steward leads spec-dialog session {SessionId}; its awaiting proposal is cleared", state.JobId);
            if (!DialogTarget.IsDashboard(state)) await notices.TellAsync(state, composer.ComposeSuperseded(), cancellationToken);
        }
        if (proposal is AnswerOutcome) return;
        (string Id, string? Replaced) stored;
        try
        {
            stored = await proposals.StoreAwaitingAsync(state, proposal, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "The proposal of spec-dialog session {SessionId} could not be stored — not shown", state.JobId);
            await notices.TellAsync(state, replies.ComposeTurnFailed("the proposal could not be stored, so it is not shown"), cancellationToken);
            return;
        }
        await (turnProposals?.WriteAsync(state, runId, proposal, cancellationToken) ?? Task.CompletedTask);
        if (stored.Replaced is not null && !DialogTarget.IsDashboard(state)) await notices.TellAsync(state, composer.ComposeSuperseded(), cancellationToken);
        await outcomeChannel.ProposeAsync(state, proposal, cancellationToken, stored.Id);
        await chatCards.PostAsync(state, proposal, stored.Id, cancellationToken);
        logger.LogInformation("Outcome {Kind} for spec-dialog session {SessionId} awaits a decision as {ProposalId}",
            proposal.GetType().Name, state.JobId, stored.Id);
    }
}
