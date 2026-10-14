namespace AgentSmith.Contracts.Models;

/// <summary>2026-10-13-8762i: the reserved keys of a proposal's review record. A spec id is a minted date,
/// so <see cref="Series"/> can never be one.</summary>
public static class ProposalReviewTokens
{
    /// <summary>The key of a design turn's series round and the <see cref="OutcomeProposal.NotReviewed"/>
    /// entry for a series contract not reviewed yet.</summary>
    public const string Series = "series";
}
