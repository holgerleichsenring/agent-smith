using System.Text;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-11-12-92e9b: what the review of the design partner's final draft found, as the message that hands it back
/// before the reply goes out — headed as a review report, never as the operator. Null when the pass reviewed nothing:
/// no verdict and no finding beyond "not reviewed yet" (the cost cap) or a list no reviewer reads. 2026-11-13-3f79:
/// only what the pass itself took — its own rounds and the findings on the drafts it attacked; a settled or carried
/// round stays on the proposal but is not reported again. 2026-11-13-67ae: a list pass always reports — clean, its
/// findings, or that the plan reviewer gave no answer — unless the conversation is bound to a ticket.
/// 2026-11-13-b3a2: drafts refused on owner decisions only are listed apart as waiting on the owner.
/// </summary>
public static class DesignTurnReviewReport
{
    public const string Heading = "## Review report (from fresh reviewers of your final draft, not from the operator)";

    public const string Rule =
        "Your reply has not gone out yet. Weigh each objection: change the draft where it holds, and say in your "
        + "reply which objections stand and why.";

    public const string WaitingHeading = "Waits on the owner:";

    public const string WaitingRule =
        "These drafts stay as written until the owner answers: each objection below is a decision only the owner can "
        + "make. Put each decision in your open questions with your proposal for it.";

    public const string OwnerQuestion = "the owner decides:";

    public const string CleanList = "The reviewers read the final list of tickets and found nothing to object to.";

    /// <param name="reviewed">The pass's reviewed proposal.</param>
    /// <param name="carried">Every round known before the pass (by reference); none of them is reported.</param>
    public static string? For(OutcomeProposal reviewed, IEnumerable<ProposalReviewRound> carried)
    {
        ArgumentNullException.ThrowIfNull(reviewed);
        ArgumentNullException.ThrowIfNull(carried);
        if (reviewed is TicketGraphOutcome list) return ForList(list);
        var known = new HashSet<ProposalReviewRound>(carried, ReferenceEqualityComparer.Instance);
        var rounds = reviewed.ReviewRounds.Where(round => !known.Contains(round)).ToList();
        var waiting = rounds.Where(ReviewOwnerDecision.Waits).Select(round => round.SpecId).ToHashSet(StringComparer.Ordinal);
        var findings = reviewed.Findings.Where(f => Reviewed(f) && Attacked(reviewed, rounds, f)).ToList();
        if (rounds.Count == 0 && findings.Count == 0) return null;
        var objections = findings.Where(f => !waiting.Contains(f.SpecId)).ToList();
        var sb = new StringBuilder().AppendLine(Heading);
        if (objections.Count > 0 || rounds.Any(round => !waiting.Contains(round.SpecId))) sb.AppendLine(Rule);
        foreach (var round in rounds.Where(round => !waiting.Contains(round.SpecId)))
        {
            sb.AppendLine($"- {round.SpecId}: {round.Verdict}");
            foreach (var cut in round.Cuts)
                sb.AppendLine($"  cut {cut.Register}: \"{cut.Quote}\" -> {(cut.Replacement is null ? "removed" : $"\"{cut.Replacement}\"")}");
        }
        foreach (var finding in objections)
        {
            sb.AppendLine($"- {finding.SpecId} [{finding.Problem}]: {finding.Why}");
            if (!string.IsNullOrWhiteSpace(finding.Decision))
                sb.AppendLine($"  {OwnerQuestion} {ReviewOwnerDecision.Question(finding.Decision)}");
        }
        if (waiting.Count > 0) AppendWaiting(sb, findings.Where(f => waiting.Contains(f.SpecId)).ToList(), waiting);
        if (reviewed.NotReviewed is { Count: > 0 } open) sb.AppendLine($"Not reviewed: {string.Join(", ", open)}");
        return sb.ToString();
    }

    /// <summary>2026-11-13-b3a2: drafts refused on owner decisions only, apart, each decision as a question.</summary>
    private static void AppendWaiting(StringBuilder sb, IReadOnlyList<ProposalFinding> findings, IReadOnlySet<string> waiting)
    {
        sb.AppendLine(WaitingHeading).AppendLine(WaitingRule);
        foreach (var specId in waiting)
        {
            sb.AppendLine($"- {specId}:");
            foreach (var finding in findings.Where(f => f.SpecId == specId && !string.IsNullOrWhiteSpace(f.Decision)))
                sb.AppendLine($"  {ReviewOwnerDecision.Question(finding.Decision!)} ({finding.Why})");
        }
    }

    /// <summary>2026-11-13-67ae: a list pass with no finding is reported clean; a bound conversation's list is not.</summary>
    private static string? ForList(TicketGraphOutcome list)
    {
        if (list.Findings.Any(f => f.Problem == TicketPlanReview.BoundProblem)) return null;
        var sb = new StringBuilder().AppendLine(Heading);
        if (list.Findings.Count == 0) return sb.AppendLine(CleanList).ToString();
        if (list.Findings.Any(Reviewed)) sb.AppendLine(Rule);
        foreach (var finding in list.Findings) sb.AppendLine($"- {finding.SpecId} [{finding.Problem}]: {finding.Why}");
        return sb.ToString();
    }

    /// <summary>A spec's finding counts when the pass took a round on its draft, a parent's and a series-contract finding when it took the series round.</summary>
    private static bool Attacked(OutcomeProposal reviewed, IReadOnlyList<ProposalReviewRound> rounds, ProposalFinding finding)
    {
        if (rounds.Any(round => round.SpecId == finding.SpecId)) return true;
        return rounds.Any(round => round.SpecId == ProposalReviewTokens.Series)
            && (finding.Problem == ProposalSeriesRound.Problem || (reviewed as EpicOutcome)?.Parent.SpecId == finding.SpecId);
    }

    private static bool Reviewed(ProposalFinding finding) =>
        finding.Problem is not (ProposalReviewSummary.NotReviewedYet or TicketPlanReview.BoundProblem);
}
