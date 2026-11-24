namespace AgentSmith.Application.Models;

/// <summary>
/// p0496: the outcome of merging a repository's base branch into a reused work branch.
/// Every status leaves the sandbox with a clean tree — a conflict is aborted before this
/// result is handed back — so no caller can stage, commit or push a tree that carries
/// conflict markers.
/// </summary>
public sealed record BaseMergeResult(
    BaseMergeStatus Status,
    string? BaseRef,
    IReadOnlyList<string> ConflictingPaths,
    string? Reason)
{
    public static BaseMergeResult Unavailable(string reason) =>
        new(BaseMergeStatus.Unavailable, BaseRef: null, [], reason);

    public static BaseMergeResult UpToDate(string baseRef) =>
        new(BaseMergeStatus.UpToDate, baseRef, [], Reason: null);

    public static BaseMergeResult Merged(string baseRef) =>
        new(BaseMergeStatus.Merged, baseRef, [], Reason: null);

    /// <summary>2026-11-24-6811c: a master resolved the conflicts the unions left — a merge no rule produced, which the
    /// refresh only pushes once a build and test stage checked it.</summary>
    public bool ResolvedByMaster { get; init; }

    /// <summary>2026-11-24-9dc0a: the paths still open after the unions and any master, and the master's reason.</summary>
    public static BaseMergeResult Conflicted(string baseRef, IReadOnlyList<string> conflictingPaths, string? reason = null) =>
        new(BaseMergeStatus.Conflicted, baseRef, conflictingPaths, reason);
}
