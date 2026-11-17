using System.Text;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-11-17-5701a: the spec section of a design turn's review report — each round the pass itself took with its cuts,
/// the objections on the drafts it attacked, the drafts waiting on the owner and what it could not review — for a spec,
/// an epic or a ticket list's specs alike. Each round names the draft file an edit of its cuts names. Null when the
/// pass took nothing on a spec.
/// </summary>
internal static class DesignTurnSpecReport
{
    /// <summary>The section's lines, and whether they carry an objection the master weighs.</summary>
    public static (bool Objects, string Lines)? Of(OutcomeProposal reviewed, IEnumerable<ProposalReviewRound> carried)
    {
        var known = new HashSet<ProposalReviewRound>(carried, ReferenceEqualityComparer.Instance);
        var rounds = reviewed.ReviewRounds.Where(round => !known.Contains(round)).ToList();
        var waiting = rounds.Where(ReviewOwnerDecision.Waits).Select(round => round.SpecId).ToHashSet(StringComparer.Ordinal);
        var findings = reviewed.Findings.Where(f => Reviewed(f) && Attacked(reviewed, rounds, f)).ToList();
        if (rounds.Count == 0 && findings.Count == 0) return null;
        var objections = findings.Where(f => !waiting.Contains(f.SpecId)).ToList();
        var taken = rounds.Where(round => !waiting.Contains(round.SpecId)).ToList();
        var sb = new StringBuilder();
        AppendRounds(sb, reviewed, taken);
        AppendObjections(sb, objections);
        if (waiting.Count > 0) AppendWaiting(sb, findings.Where(f => waiting.Contains(f.SpecId)).ToList(), waiting);
        if (reviewed.NotReviewed is { Count: > 0 } open) sb.AppendLine($"Not reviewed: {string.Join(", ", open)}");
        return (objections.Count > 0 || taken.Count > 0, sb.ToString());
    }

    private static void AppendRounds(StringBuilder sb, OutcomeProposal reviewed, IReadOnlyList<ProposalReviewRound> rounds)
    {
        foreach (var round in rounds)
        {
            sb.AppendLine($"- {round.SpecId}{File(reviewed, round.SpecId)}: {round.Verdict}");
            foreach (var cut in round.Cuts)
                sb.AppendLine($"  cut {cut.Register}: \"{cut.Quote}\" -> \"{cut.Replacement}\"");
        }
    }

    private static void AppendObjections(StringBuilder sb, IReadOnlyList<ProposalFinding> objections)
    {
        foreach (var finding in objections)
        {
            sb.AppendLine($"- {finding.SpecId} [{finding.Problem}]: {finding.Why}");
            if (!string.IsNullOrWhiteSpace(finding.Decision))
                sb.AppendLine($"  {DesignTurnReviewReport.OwnerQuestion} {ReviewOwnerDecision.Question(finding.Decision)}");
        }
    }

    /// <summary>2026-11-15-06fc: the draft file a spec's round reviewed, which an edit_file of its cuts names;
    /// 2026-11-17-5701a: a ticket's spec under its ticket.</summary>
    private static string File(OutcomeProposal reviewed, string specId)
    {
        if (specId == ProposalReviewTokens.Series) return string.Empty;
        var path = ProposalSpecSet.TicketOf(reviewed, specId) is { } ticket
            ? Writes.DraftPath.OfTicketSpec(ticket.Key, specId)
            : Writes.DraftPath.OfSpec(specId, parent: false);
        return $" ({Writes.DraftPath.Shown(path)})";
    }

    /// <summary>2026-11-13-b3a2: drafts refused on owner decisions only, apart, each decision as a question.</summary>
    private static void AppendWaiting(StringBuilder sb, IReadOnlyList<ProposalFinding> findings, IReadOnlySet<string> waiting)
    {
        sb.AppendLine(DesignTurnReviewReport.WaitingHeading).AppendLine(DesignTurnReviewReport.WaitingRule);
        foreach (var specId in waiting)
        {
            sb.AppendLine($"- {specId}:");
            foreach (var finding in findings.Where(f => f.SpecId == specId && !string.IsNullOrWhiteSpace(f.Decision)))
                sb.AppendLine($"  {ReviewOwnerDecision.Question(finding.Decision!)} ({finding.Why})");
        }
    }

    /// <summary>A spec's finding counts when the pass took a round on its draft, a parent's and a series-contract finding when it took the series round.</summary>
    private static bool Attacked(OutcomeProposal reviewed, IReadOnlyList<ProposalReviewRound> rounds, ProposalFinding finding)
    {
        if (rounds.Any(round => round.SpecId == finding.SpecId)) return true;
        return rounds.Any(round => round.SpecId == ProposalReviewTokens.Series)
            && (finding.Problem == ProposalSeriesRound.Problem || (reviewed as EpicOutcome)?.Parent.SpecId == finding.SpecId);
    }

    public static bool Reviewed(ProposalFinding finding) =>
        finding.Problem is not (ProposalReviewSummary.NotReviewedYet or TicketPlanReview.BoundProblem);
}
