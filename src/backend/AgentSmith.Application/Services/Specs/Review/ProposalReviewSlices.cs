using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-11-12-92e9b: a proposal composed from the ledger's results for its texts. When one pass reviewed every text,
/// its review is taken whole; otherwise each text brings its own part — a spec its findings, rounds, not-reviewed mark
/// and clean text by its id, an epic's parent its findings and the series round.
/// </summary>
internal static class ProposalReviewSlices
{
    public static OutcomeProposal Compose(OutcomeProposal proposal, IReadOnlyList<(string Key, OutcomeProposal Result)> parts)
    {
        if (parts.Count == 0) return proposal;
        if (parts.Select(p => p.Result).Distinct(ReferenceEqualityComparer.Instance).Count() == 1)
            return Take(proposal, parts[0].Result);
        var ids = parts.Select(p => (Id: IdOf(proposal, p.Key), p.Result)).ToList();
        var notReviewed = ids.SelectMany(p => (p.Result.NotReviewed ?? []).Where(id => id == p.Id)).ToList();
        return proposal with
        {
            Findings = [.. ids.SelectMany(p => p.Result.Findings.Where(f => f.SpecId == FindingId(proposal, p.Id)))],
            ReviewRounds = [.. ids.SelectMany(p => p.Result.ReviewRounds.Where(r => r.SpecId == p.Id))],
            NotReviewed = notReviewed.Count == 0 ? null : notReviewed,
            ReviewedClean = [.. ids.SelectMany(p => p.Result.ReviewedClean.Where(c => c.SpecId == p.Id))],
        };
    }

    private static OutcomeProposal Take(OutcomeProposal proposal, OutcomeProposal result) => proposal with
    {
        Findings = result.Findings, ReviewRounds = result.ReviewRounds, NotReviewed = result.NotReviewed,
        ReviewedClean = result.ReviewedClean,
    };

    /// <summary>The id a text's rounds, marks and clean texts sit under: a draft's own, the series token for a parent.</summary>
    private static string IdOf(OutcomeProposal proposal, string key) =>
        proposal is EpicOutcome epic && key == ProposalReviewTexts.Parent(epic)
            ? ProposalReviewTokens.Series
            : ProposalSpecSet.Drafts(proposal).First(d => key == ProposalReviewTexts.Draft(d)).SpecId;

    /// <summary>A parent's findings sit under the parent's id; a draft's under its own.</summary>
    private static string FindingId(OutcomeProposal proposal, string id) =>
        id == ProposalReviewTokens.Series && proposal is EpicOutcome epic ? epic.Parent.SpecId : id;
}
