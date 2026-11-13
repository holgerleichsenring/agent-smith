using System.Text;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-11-12-92e9b: what the review of the design partner's final draft found, as the message that hands it back
/// before the reply goes out — headed as a review report, never as the operator. Null when the pass reviewed nothing:
/// no verdict and no finding beyond "not reviewed yet" (the cost cap) or a list no reviewer reads. 2026-11-13-3f79:
/// only what the pass itself took — its own rounds and the findings on the drafts it attacked; a settled or carried
/// round stays on the proposal but is not reported again.
/// </summary>
public static class DesignTurnReviewReport
{
    public const string Heading = "## Review report (from fresh reviewers of your final draft, not from the operator)";

    public const string Rule =
        "Your reply has not gone out yet. Weigh each objection: change the draft where it holds, and say in your "
        + "reply which objections stand and why.";

    /// <param name="reviewed">The pass's reviewed proposal.</param>
    /// <param name="carried">Every round known before the pass (by reference); none of them is reported.</param>
    public static string? For(OutcomeProposal reviewed, IEnumerable<ProposalReviewRound> carried)
    {
        ArgumentNullException.ThrowIfNull(reviewed);
        ArgumentNullException.ThrowIfNull(carried);
        var known = new HashSet<ProposalReviewRound>(carried, ReferenceEqualityComparer.Instance);
        var rounds = reviewed.ReviewRounds.Where(round => !known.Contains(round)).ToList();
        var findings = reviewed.Findings.Where(f => Reviewed(f) && Attacked(reviewed, rounds, f)).ToList();
        if (rounds.Count == 0 && findings.Count == 0) return null;
        var sb = new StringBuilder().AppendLine(Heading).AppendLine(Rule);
        foreach (var round in rounds)
        {
            sb.AppendLine($"- {round.SpecId}: {round.Verdict}");
            foreach (var cut in round.Cuts)
                sb.AppendLine($"  cut {cut.Register}: \"{cut.Quote}\" -> {(cut.Replacement is null ? "removed" : $"\"{cut.Replacement}\"")}");
        }
        foreach (var finding in findings) sb.AppendLine($"- {finding.SpecId} [{finding.Problem}]: {finding.Why}");
        if (reviewed.NotReviewed is { Count: > 0 } open) sb.AppendLine($"Not reviewed: {string.Join(", ", open)}");
        return sb.ToString();
    }

    /// <summary>A list the pass ran over is attacked whole; a spec's finding counts when the pass took a round on its
    /// draft, a parent's and a series-contract finding when it took the series round.</summary>
    private static bool Attacked(OutcomeProposal reviewed, IReadOnlyList<ProposalReviewRound> rounds, ProposalFinding finding)
    {
        if (reviewed is TicketGraphOutcome) return true;
        if (rounds.Any(round => round.SpecId == finding.SpecId)) return true;
        return rounds.Any(round => round.SpecId == ProposalReviewTokens.Series)
            && (finding.Problem == ProposalSeriesRound.Problem || (reviewed as EpicOutcome)?.Parent.SpecId == finding.SpecId);
    }

    private static bool Reviewed(ProposalFinding finding) =>
        finding.Problem is not (ProposalReviewSummary.NotReviewedYet or TicketPlanReview.BoundProblem);
}
