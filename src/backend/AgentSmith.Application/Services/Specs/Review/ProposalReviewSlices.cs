using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-11-12-92e9b: a proposal composed from the ledger's results for its texts. When one pass reviewed every text,
/// its review is taken whole; otherwise each text brings its own part — a spec its findings, rounds, not-reviewed mark
/// and clean text by its id, an epic's parent its findings and the series round. 2026-11-13-3f79: a settled text
/// edited since (<c>Edited</c>) brings its settled rounds only — not the findings its cuts answered, never a clean
/// text, since the text it was reviewed in is gone; an edited settled list keeps only the bound-conversation note.
/// </summary>
internal static class ProposalReviewSlices
{
    public static OutcomeProposal Compose(
        OutcomeProposal proposal, IReadOnlyList<(ProposalReviewText Text, OutcomeProposal Result, bool Edited)> parts)
    {
        if (parts.Count == 0) return proposal;
        if (parts.All(p => !p.Edited) && parts.Select(p => p.Result).Distinct(ReferenceEqualityComparer.Instance).Count() == 1)
            return Take(proposal, parts[0].Result);
        if (proposal is TicketGraphOutcome graph) return EditedList(graph, parts[0].Result);
        var exact = parts.Where(p => !p.Edited).ToList();
        var notReviewed = exact.SelectMany(p => (p.Result.NotReviewed ?? []).Where(id => id == p.Text.Id)).ToList();
        return proposal with
        {
            Findings = [.. parts.SelectMany(p => Findings(proposal, p.Text, p.Result, p.Edited))],
            ReviewRounds = [.. parts.SelectMany(p => p.Result.ReviewRounds.Where(r => r.SpecId == p.Text.Id))],
            NotReviewed = notReviewed.Count == 0 ? null : notReviewed,
            ReviewedClean = [.. exact.SelectMany(p => p.Result.ReviewedClean.Where(c => c.SpecId == p.Text.Id))],
        };
    }

    private static OutcomeProposal Take(OutcomeProposal proposal, OutcomeProposal result) => proposal with
    {
        Findings = result.Findings, ReviewRounds = result.ReviewRounds, NotReviewed = result.NotReviewed,
        ReviewedClean = result.ReviewedClean,
    };

    private static OutcomeProposal EditedList(TicketGraphOutcome graph, OutcomeProposal result) => graph with
    {
        Findings = [.. result.Findings.Where(f => f.Problem == TicketPlanReview.BoundProblem)],
        ReviewRounds = [], NotReviewed = null, ReviewedClean = [],
    };

    private static IEnumerable<ProposalFinding> Findings(
        OutcomeProposal proposal, ProposalReviewText text, OutcomeProposal result, bool edited)
    {
        var id = FindingId(proposal, text.Id);
        return result.Findings.Where(f => f.SpecId == id && !(edited && Answered(f)));
    }

    /// <summary>A reviewer's objection — a round's uncleared finding or a series-contract one — which the applied
    /// cuts answered.</summary>
    private static bool Answered(ProposalFinding finding) => finding.Problem
        is SpecReviewVerdicts.Build or SpecReviewVerdicts.BuildWithCuts or SpecReviewVerdicts.Refuse or ProposalSeriesRound.Problem;

    /// <summary>A parent's findings sit under the parent's id; a draft's under its own.</summary>
    private static string FindingId(OutcomeProposal proposal, string id) =>
        id == ProposalReviewTokens.Series && proposal is EpicOutcome epic ? epic.Parent.SpecId : id;
}
