using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762i: a design turn's review as it is left on the proposal. Rounds in draft order; what still
/// stands — every evidence problem, the findings of a draft its review did not call clean, and one "not reviewed
/// yet" finding per spec id not reviewed, so a chat confirmation lists it too. 2026-11-12-cd4ch: advice only — the
/// proposal is the turn's own, and a cut stays on its round for the operator to read.
/// </summary>
internal static class ProposalReviewSummary
{
    public const string NotReviewedYet = "not reviewed yet";
    /// <summary>2026-11-10-18cf: the finding names why the review did not finish. 2026-11-10-8e59: information, not
    /// a hold — the spec is reviewed in its exact text before it is built.</summary>
    public static string NotReviewedWhy(string reason) =>
        $"not yet reviewed ({reason}); it will be reviewed before it is built";

    /// <summary>2026-11-10-18cf: <paramref name="proposal"/> with every one of <paramref name="drafts"/> marked not
    /// reviewed for <paramref name="reason"/>, beside <paramref name="findings"/>.</summary>
    public static OutcomeProposal MarkedNotReviewed(
        OutcomeProposal proposal, IReadOnlyList<SpecDraft> drafts, IReadOnlyList<ProposalFinding> findings, string reason) =>
        proposal with
        {
            NotReviewed = drafts.Count == 0 ? null : [.. drafts.Select(d => d.SpecId)],
            Findings = [.. findings, .. drafts.Select(d => new ProposalFinding(d.SpecId, NotReviewedYet, NotReviewedWhy(reason)))],
        };

    public const string NoAnswerWhy = "no reviewer answered (each call failed, timed out or could not be read)";
    public const string UnderReviewWhy = "it was still under review when this was published, and a turn that ends here leaves it unreviewed";
    public const string UnreadWhy = "the reviewers could not read what this spec names";

    /// <summary>The review as it stands after the pass.</summary>
    public static ProposalReviewOutcome Of(ProposalReviewSession session, IReadOnlyList<SpecEvidenceProblem> parentProblems)
    {
        var order = ProposalSpecSet.Drafts(session.Proposal).Select(d => d.SpecId).ToList();
        var drafts = session.Drafts.OrderBy(d => order.IndexOf(d.SpecId)).ToList();
        var unreviewed = drafts.Where(d => d.NotReviewed).ToList();
        var standing = Problems(ParentId(session.Proposal), parentProblems)
            .Concat(drafts.SelectMany(d => Problems(d.SpecId, d.Evidence.Problems).Concat(Uncleared(d))))
            .Concat(unreviewed.Select(d => new ProposalFinding(d.SpecId, NotReviewedYet, NotReviewedWhy(d.StoppedBecause ?? NoAnswerWhy))))
            .ToList();
        return new ProposalReviewOutcome(session.Proposal, [.. drafts.SelectMany(d => d.Rounds)],
            unreviewed.Count == 0 ? null : [.. unreviewed.Select(d => d.SpecId)], standing)
        {
            ReviewedClean = [.. drafts.Where(d => d.ReviewedClean).Select(d => Contracts.Specs.ReviewedText.Of(d.SpecId, d.Draft.Yaml))],
        };
    }

    private static IEnumerable<ProposalFinding> Problems(string? specId, IReadOnlyList<SpecEvidenceProblem> problems) =>
        specId is null ? [] : problems.Select(p => new ProposalFinding(specId, ProposalEvidenceReview.Problem, p.Problem, p.Claim));

    private static IEnumerable<ProposalFinding> Uncleared(ProposalDraftReview draft) =>
        !draft.Settled && draft.Rounds is [.., var last] && last.Verdict != SpecReviewVerdicts.Build
            ? last.Findings.Select(f => new ProposalFinding(draft.SpecId, last.Verdict, f.Text, null, f.Evidence))
            : [];

    private static string? ParentId(OutcomeProposal proposal) => (proposal as EpicOutcome)?.Parent.SpecId;
}
