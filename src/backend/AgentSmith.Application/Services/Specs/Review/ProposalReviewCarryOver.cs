using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762i: what a design turn keeps of the review the proposal it revises already had
/// (<see cref="ContextKeys.SpecDialogRevisedProposal"/>). A draft byte-identical to one that has rounds there
/// and is not in its NotReviewed keeps those rounds and counts as stopped; the drafts it listed as not
/// reviewed go first. A series round is never carried: its review is taken fresh each turn. 2026-11-12-92e9b: within
/// the turn, a draft whose exact text a pass already reviewed (<see cref="ProposalReviewLedger"/>) carries that pass's
/// result instead, so an unchanged text is not attacked twice in one turn. 2026-11-13-3f79: a draft a pass of this
/// turn passed and that was edited since is settled — stopped with its settled rounds, its new text never marked
/// attacked — so neither review_proposal nor the end pass attacks it again this turn.
/// </summary>
internal static class ProposalReviewCarryOver
{
    public static List<ProposalDraftReview> Seed(PipelineContext pipeline, IReadOnlyList<SpecDraft> drafts)
    {
        var revised = pipeline.TryGet<OutcomeProposal>(ContextKeys.SpecDialogRevisedProposal, out var r) ? r : null;
        var ledger = ProposalReviewLedger.Of(pipeline);
        var reviews = drafts.Select(draft => (Draft: draft, Exact: ledger.For(draft), Settled: ledger.Settled(draft.SpecId)))
            .Select(d => (d.Draft, Before: d.Exact ?? revised, Settled: d.Exact is null ? d.Settled : null)).ToList();
        return [.. reviews.Select(d => d.Settled is { } settled
                ? Settle(new ProposalDraftReview(d.Draft), settled)
                : Carried(new ProposalDraftReview(d.Draft), d.Before, d.Before?.NotReviewed ?? []))
            .OrderBy(review => Waiting(reviews, review.SpecId) ? 0 : 1)];
    }

    private static bool Waiting(IReadOnlyList<(SpecDraft Draft, OutcomeProposal? Before, OutcomeProposal? Settled)> reviews, string specId) =>
        reviews.Any(d => d.Settled is null && d.Draft.SpecId == specId
            && (d.Before?.NotReviewed ?? []).Contains(specId, StringComparer.Ordinal));

    private static ProposalDraftReview Settle(ProposalDraftReview review, OutcomeProposal settled)
    {
        review.Rounds.AddRange(settled.ReviewRounds.Where(round => round.SpecId == review.SpecId));
        review.Stopped = true;
        review.Settled = true;
        return review;
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
        // 2026-11-10-8e59: a clean review carries over only for exactly the text it was on.
        if (ReviewedText.Covers(revised.ReviewedClean, review.SpecId, review.Draft.Yaml)) review.AttackedYaml = review.Draft.Yaml;
        return review;
    }
}
