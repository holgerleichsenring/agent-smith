using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>2026-10-13-8762i: a re-proposal merged onto the last valid proposal — the drafts the guard
/// admitted, the ids it reverted to their last valid text, and whether the reply changed any draft at all.
/// 2026-11-11-2bdfb: an epic re-cut also adds and drops children; a reply the guard cannot use at all is void,
/// with its reason, and <see cref="Merged"/> is then the last valid proposal.</summary>
public sealed record ProposalGuardResult(OutcomeProposal Merged, IReadOnlyList<string> Reverted, bool Changed)
{
    /// <summary>Children the re-cut added, in the merged set and open for the next round.</summary>
    public IReadOnlyList<string> Added { get; init; } = [];

    /// <summary>Children the re-cut dropped, gone from the merged set.</summary>
    public IReadOnlyList<string> Dropped { get; init; } = [];

    /// <summary>Why the reply was not used as a whole, or null.</summary>
    public string? Void { get; init; }

    public static ProposalGuardResult Voided(OutcomeProposal last, string why) => new(last, [], false) { Void = why };
}
