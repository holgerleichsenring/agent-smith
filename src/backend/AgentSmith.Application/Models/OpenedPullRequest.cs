namespace AgentSmith.Application.Models;

/// <summary>
/// Represents the outcome of a per-repo PR-open attempt within a multi-repo
/// pipeline run. Published as ContextKeys.OpenedPullRequests so p0158c's
/// PATCH pass can read the list and update each opened PR's body with the
/// sibling URL list.
/// </summary>
public sealed record OpenedPullRequest(
    string RepoName,
    string? Url,
    OpenStatus Status,
    string? Reason = null);

public enum OpenStatus
{
    Opened,
    SkippedNoChanges,
    Failed
}
