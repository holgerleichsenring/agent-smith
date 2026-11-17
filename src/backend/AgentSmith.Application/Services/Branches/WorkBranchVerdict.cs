using AgentSmith.Contracts.Providers;

namespace AgentSmith.Application.Services.Branches;

/// <summary>
/// 2026-11-17-713ec: whether a branch-cleanup run deletes an agent-smith/ branch. Only a branch
/// whose pull request has ended — merged or closed — and that still points where that pull request
/// left it: a branch something targets, one with an open pull request, one that never had one, and
/// one pushed to since are kept, each with the reason the run reports.
/// </summary>
public static class WorkBranchVerdict
{
    public const string TargetedByAnOpenPullRequest = "an open pull request targets it";
    public const string HasAnOpenPullRequest = "its pull request is open";
    public const string NoPullRequest = "no pull request was opened from it";
    public const string MovedSince = "commits were pushed after its pull request ended";

    /// <summary>Null to delete; otherwise the reason the branch stays.</summary>
    public static string? KeepBecause(WorkBranchTip tip, BranchPullRequests pullRequests)
    {
        ArgumentNullException.ThrowIfNull(tip);
        ArgumentNullException.ThrowIfNull(pullRequests);
        if (pullRequests.OpenInto > 0) return TargetedByAnOpenPullRequest;
        if (pullRequests.From.Any(p => p.State == BranchPullRequestState.Open)) return HasAnOpenPullRequest;
        if (pullRequests.From.Count == 0) return NoPullRequest;
        return pullRequests.From.Any(p => string.Equals(p.HeadSha, tip.Sha, StringComparison.OrdinalIgnoreCase))
            ? null
            : MovedSince;
    }
}
