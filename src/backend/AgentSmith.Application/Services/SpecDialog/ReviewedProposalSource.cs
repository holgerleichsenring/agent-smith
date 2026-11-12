using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-11-12-cd4cj: the ONE place the review_proposal tool reads the proposal it reviews — the turn's published
/// outcome, else the draft the design tools have written so far in the per-turn store (2026-11-12-cd4ca), which starts
/// from the proposal awaiting the operator's decision.
/// </summary>
public static class ReviewedProposalSource
{
    public static OutcomeProposal? Current(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return pipeline.TryGet<OutcomeProposal>(ContextKeys.SpecDialogOutcome, out var outcome) && outcome is not null ? outcome
            : pipeline.TryGet<SpecDialogReplySlot>(ContextKeys.SpecDialogReplySlot, out var slot) && slot is not null
                ? Writes.ProposalDraftOutcome.Of(slot.Proposal.Current) : null;
    }
}
