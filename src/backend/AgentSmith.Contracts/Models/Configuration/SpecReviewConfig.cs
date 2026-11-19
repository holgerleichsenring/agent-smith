namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-10-13-8762g: agentsmith.yml's top-level <c>spec_review:</c> block. 2026-11-19-4176: no step of a run reviews a
/// spec any more (9c1cb, 3e10), so the fields that sized that review are retired; what is left sizes the entry
/// re-ground of a spec's citations. Global by design: the machinery has no project dimension.
/// </summary>
public sealed class SpecReviewConfig
{
    /// <summary>2026-11-17-f9b0b: the distinct (repository, path) pairs, each candidate read counted as one more, one
    /// entered spec's citations are re-grounded against; a citation past it is left unchecked.</summary>
    public int RegroundReadBudget { get; set; } = 24;

    /// <summary>What is wrong with these values, one sentence each; empty when nothing is. The ONE rule —
    /// a run refuses what this names, never clamping it.</summary>
    public IReadOnlyList<string> Problems() =>
        RegroundReadBudget < 1
            ? [$"spec_review.reground_read_budget is {RegroundReadBudget}; it must be 1 or more"]
            : [];
}
