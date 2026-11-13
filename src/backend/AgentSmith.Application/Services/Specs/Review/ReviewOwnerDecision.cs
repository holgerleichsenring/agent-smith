using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-11-13-b3a2: an objection only the owner can settle. A refused round whose every finding carries a decision
/// means the draft waits on the owner — it is not wrong, and is settled for the turn as a passed draft is; a round
/// that mixes a decision with a defect is an ordinary refusal.
/// </summary>
public static class ReviewOwnerDecision
{
    public static bool Carries(SpecReviewFinding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);
        return !string.IsNullOrWhiteSpace(finding.Decision);
    }

    public static bool Waits(ProposalReviewRound round)
    {
        ArgumentNullException.ThrowIfNull(round);
        return round.SpecId != ProposalReviewTokens.Series && round.Verdict == SpecReviewVerdicts.Refuse
            && round.Findings.Count > 0 && round.Findings.All(Carries);
    }

    /// <summary>A decision worded as the question it is, ending in a question mark.</summary>
    public static string Question(string decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        var text = decision.Trim();
        return text.EndsWith('?') ? text : $"{text.TrimEnd('.')}?";
    }
}
