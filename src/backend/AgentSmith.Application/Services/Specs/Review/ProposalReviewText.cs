using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-11-13-3f79: one text a review pass reads — its exact <paramref name="Key"/>, the <paramref name="Id"/> its
/// rounds, marks and clean texts sit under (a draft's own id, the series token for an epic's parent, "list" for a
/// ticket list), and the <paramref name="SettledKey"/> it stays settled under for the rest of the turn once a pass
/// passed it, whatever the text becomes.
/// </summary>
internal sealed record ProposalReviewText(string Key, string Id, string SettledKey)
{
    /// <summary>
    /// Whether <paramref name="result"/> passed this text: a draft whose last round there said build or build with
    /// cuts; a parent whose last series round did, or any recorded parent of an epic with no contract; any recorded list.
    /// 2026-11-13-b3a2: a draft whose last round refused on owner decisions only waits on the owner and counts as passed.
    /// </summary>
    public bool PassedIn(OutcomeProposal result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (Id == ProposalReviewTexts.ListId) return true;
        if (Id == ProposalReviewTokens.Series && result is EpicOutcome { Contract: null or { IsEmpty: true } }) return true;
        var last = result.ReviewRounds.LastOrDefault(round => round.SpecId == Id);
        return last?.Verdict is SpecReviewVerdicts.Build or SpecReviewVerdicts.BuildWithCuts
            || (last is not null && ReviewOwnerDecision.Waits(last));
    }
}
