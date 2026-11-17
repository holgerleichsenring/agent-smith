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
/// 2026-11-15-06fc: each reviewed spec names its draft file, and a cut is a quote and its replacement ("" removes it).
/// 2026-11-17-5701a: a ticket list reports its plan findings and its specs' section (<see cref="DesignTurnSpecReport"/>);
/// in a bound conversation only the plan section is dropped.
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
        var plan = reviewed is TicketGraphOutcome list ? Plan(list) : null;
        var specs = DesignTurnSpecReport.Of(reviewed, carried);
        if (plan is null && specs is null) return null;
        var sb = new StringBuilder().AppendLine(Heading);
        if (plan?.Objects == true || specs?.Objects == true) sb.AppendLine(Rule);
        return sb.Append(plan?.Lines).Append(specs?.Lines).ToString();
    }

    /// <summary>2026-11-13-67ae: a list pass with no finding is reported clean; a bound conversation's list is not.
    /// 2026-11-17-5701a: only the findings on no spec of the list — its specs are the spec section's.</summary>
    private static (bool Objects, string Lines)? Plan(TicketGraphOutcome list)
    {
        var findings = ProposalReviewSlices.ListFindings(list, edited: false);
        if (findings.Any(f => f.Problem == TicketPlanReview.BoundProblem)) return null;
        if (findings.Count == 0) return (false, CleanList + Environment.NewLine);
        var sb = new StringBuilder();
        foreach (var finding in findings) sb.AppendLine($"- {finding.SpecId} [{finding.Problem}]: {finding.Why}");
        return (findings.Any(DesignTurnSpecReport.Reviewed), sb.ToString());
    }
}
