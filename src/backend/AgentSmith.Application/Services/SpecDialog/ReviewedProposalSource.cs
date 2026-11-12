using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-11-12-cd4cj: the ONE place the review_proposal tool reads the proposal it reviews — the turn's published
/// outcome, else the proposal awaiting the operator's decision that the turn revises. When the turn's proposal lives
/// in a per-turn store written by the design tools, this read becomes the store's current draft, and nothing else
/// changes.
/// </summary>
public static class ReviewedProposalSource
{
    public static OutcomeProposal? Current(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return pipeline.TryGet<OutcomeProposal>(ContextKeys.SpecDialogOutcome, out var outcome) && outcome is not null ? outcome
            : pipeline.TryGet<OutcomeProposal>(ContextKeys.SpecDialogRevisedProposal, out var revised) ? revised : null;
    }
}
