using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>2026-11-11-2bdfb: an epic's children after a re-cut — the merged list in the reply's order, the ids
/// reverted or left out, the ids added and the ids dropped.</summary>
internal sealed record ProposalChildMerge(
    IReadOnlyList<SpecDraft> Kept, IReadOnlyList<string> Reverted, IReadOnlyList<string> Added, IReadOnlyList<string> Dropped);
