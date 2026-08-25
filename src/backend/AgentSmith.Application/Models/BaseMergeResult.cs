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

    public static BaseMergeResult Conflicted(string baseRef, IReadOnlyList<string> conflictingPaths) =>
        new(BaseMergeStatus.Conflicted, baseRef, conflictingPaths, Reason: null);
}
