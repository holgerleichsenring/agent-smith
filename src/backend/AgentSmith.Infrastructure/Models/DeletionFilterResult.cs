using AgentSmith.Contracts.Providers;

namespace AgentSmith.Infrastructure.Models;

/// <summary>
/// 2026-11-23-c81ea: a branch write's entries after the deletions of absent paths were dropped — what is left to
/// commit, the sentence for each dropped deletion, or the answer the write gives instead of committing.
/// </summary>
/// <param name="Refusal">Set when the write must answer without committing: a probe that failed, or nothing left.</param>
public sealed record DeletionFilterResult(
    IReadOnlyList<RepoFile> Files, IReadOnlyList<string> Notes, BranchWriteResult? Refusal)
{
    /// <summary><paramref name="result"/> carrying this filter's notes, when there are any.</summary>
    public BranchWriteResult WithNotes(BranchWriteResult result) =>
        Notes.Count == 0 ? result : result with { Notes = Notes };
}
