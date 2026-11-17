namespace AgentSmith.Contracts.Providers;

/// <summary>
/// 2026-11-17-713ec: a source host whose agent-smith/ branches a branch-cleanup run can read and
/// delete. A capability of the provider, as <c>IChangedPullRequestLister</c> is: a host without
/// the concept (a local repository) does not implement it, and the run skips it.
/// </summary>
public interface IWorkBranchCleanup
{
    /// <summary>Every branch under <see cref="WorkBranches.Prefix"/>/ with the commit it points at.</summary>
    Task<IReadOnlyList<WorkBranchTip>> ListWorkBranchesAsync(CancellationToken cancellationToken);

    /// <summary>The pull requests opened from the branch, in any state, and how many open ones target it.</summary>
    Task<BranchPullRequests> PullRequestsOfAsync(string branch, CancellationToken cancellationToken);

    /// <summary>Deletes the branch; false when the host refused, for instance because it moved.</summary>
    Task<bool> DeleteBranchAsync(WorkBranchTip branch, CancellationToken cancellationToken);
}

/// <summary>2026-11-17-713ec: a branch and the commit it points at.</summary>
public sealed record WorkBranchTip(string Name, string Sha);

/// <summary>2026-11-17-713ec: the state of a pull request as the branch sweep reads it.</summary>
public enum BranchPullRequestState { Open, Merged, Closed }

/// <summary>2026-11-17-713ec: one pull request from a branch — its state and the head it last carried.</summary>
public sealed record BranchPullRequest(BranchPullRequestState State, string? HeadSha);

/// <summary>2026-11-17-713ec: the pull requests from a branch, and the open ones into it.</summary>
public sealed record BranchPullRequests(IReadOnlyList<BranchPullRequest> From, int OpenInto);
