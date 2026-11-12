using System.Text;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-11-12-92e9b: what the review of the design partner's final draft found, as the message that hands it back
/// before the reply goes out — headed as a review report, never as the operator. Null when the pass reviewed nothing:
/// no verdict and no finding beyond "not reviewed yet" (the cost cap) or a list no reviewer reads.
/// </summary>
public static class DesignTurnReviewReport
{
    public const string Heading = "## Review report (from fresh reviewers of your final draft, not from the operator)";

    public const string Rule =
        "Your reply has not gone out yet. Weigh each objection: change the draft where it holds, and say in your "
        + "reply which objections stand and why.";

    public static string? For(OutcomeProposal reviewed)
    {
        ArgumentNullException.ThrowIfNull(reviewed);
        var findings = reviewed.Findings.Where(Reviewed).ToList();
        if (reviewed.ReviewRounds.Count == 0 && findings.Count == 0) return null;
        var sb = new StringBuilder().AppendLine(Heading).AppendLine(Rule);
        foreach (var round in reviewed.ReviewRounds)
        {
            sb.AppendLine($"- {round.SpecId}: {round.Verdict}");
            foreach (var cut in round.Cuts)
                sb.AppendLine($"  cut {cut.Register}: \"{cut.Quote}\" -> {(cut.Replacement is null ? "removed" : $"\"{cut.Replacement}\"")}");
        }
        foreach (var finding in findings) sb.AppendLine($"- {finding.SpecId} [{finding.Problem}]: {finding.Why}");
        if (reviewed.NotReviewed is { Count: > 0 } open) sb.AppendLine($"Not reviewed: {string.Join(", ", open)}");
        return sb.ToString();
    }

    private static bool Reviewed(ProposalFinding finding) =>
        finding.Problem is not (ProposalReviewSummary.NotReviewedYet or TicketPlanReview.BoundProblem);
}
