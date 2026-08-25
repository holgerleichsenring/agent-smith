namespace AgentSmith.Application.Models;

/// <summary>p0496: how far merging the base branch into a reused work branch got.</summary>
public enum BaseMergeStatus
{
    /// <summary>No base ref this sandbox can read — nothing was attempted.</summary>
    Unavailable,

    /// <summary>The work branch already contains the base — nothing to merge.</summary>
    UpToDate,

    /// <summary>The base's newer commits are now on the work branch.</summary>
    Merged,

    /// <summary>The merge conflicted and was aborted — the branch is exactly as it was.</summary>
    Conflicted
}
