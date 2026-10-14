using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762i: what a design turn keeps of the review the proposal it revises already had
/// (<see cref="ContextKeys.SpecDialogRevisedProposal"/>). A draft byte-identical to one that has rounds there
/// and is not in its NotReviewed keeps those rounds and counts as stopped; the drafts it listed as not
/// reviewed go first. A series round is never carried: its review is taken fresh each turn.
/// </summary>
internal static class ProposalReviewCarryOver
{
    public static List<ProposalDraftReview> Seed(PipelineContext pipeline, IReadOnlyList<SpecDraft> drafts)
    {
        var revised = pipeline.TryGet<OutcomeProposal>(ContextKeys.SpecDialogRevisedProposal, out var r) ? r : null;
        var waiting = revised?.NotReviewed ?? [];
        return [.. drafts.Select(draft => Carried(new ProposalDraftReview(draft), revised, waiting))
            .OrderBy(review => waiting.Contains(review.SpecId, StringComparer.Ordinal) ? 0 : 1)];
    }

    private static ProposalDraftReview Carried(
        ProposalDraftReview review, OutcomeProposal? revised, IReadOnlyList<string> waiting)
    {
        if (revised is null || waiting.Contains(review.SpecId, StringComparer.Ordinal)
            || !ProposalSpecSet.Drafts(revised).Any(before => before.SpecId == review.SpecId
                && string.Equals(before.Yaml, review.Draft.Yaml, StringComparison.Ordinal)))
            return review;
        review.Rounds.AddRange(revised.ReviewRounds.Where(round =>
            round.SpecId == review.SpecId && round.SpecId != ProposalReviewTokens.Series));
        review.Stopped = review.Rounds.Count > 0;
        return review;
    }
}
