namespace AgentSmith.Domain.Entities;

/// <summary>
/// One per-file change in a Diff: what was edited and how. Patch is the unified-diff
/// payload the implementation skill wrote out.
/// </summary>
public sealed record DiffChange(
    string File,
    DiffOperation Operation,
    string Summary,
    string Patch);
