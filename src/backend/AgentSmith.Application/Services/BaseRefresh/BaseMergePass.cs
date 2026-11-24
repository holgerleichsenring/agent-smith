using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.BaseRefresh;

/// <summary>2026-11-24-6811c: one pass's answer — the master's own word, its reason, and what its file tools wrote.
/// 2026-11-24-9dc0a: the word is null when the pass ended without conclude (the loop's cap, or an answer in prose); the
/// server decides whether the merge is whole, and <see cref="Session"/> lets the same conversation go on.</summary>
public sealed record BaseMergePass(bool? Whole, string? Reason, IReadOnlyList<CodeChange> Wrote)
{
    /// <summary>The conversation this pass belongs to; null for a pass no master ran (a test's stub).</summary>
    public BaseMergeSession? Session { get; init; }
}
