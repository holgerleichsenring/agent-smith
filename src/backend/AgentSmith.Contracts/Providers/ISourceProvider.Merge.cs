using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Providers;

public partial interface ISourceProvider
{
    /// <summary>
    /// 2026-11-17-023fa: merges a pull request in the protection mode asked (2026-11-25-9992i: past protection only into a
    /// series branch or for a judged how now). <see cref="BranchProtection.Bypass"/> merges now, past branch policies,
    /// protection and approval rules, as far as the token's identity may; a host that still refuses answers a refusal that
    /// names the policy, the permission to grant and the auto alternative. <see cref="BranchProtection.Respect"/> arms the host's auto-complete or
    /// auto-merge, so its rules decide when. Never throws; a local repository fast-forwards its default branch either way.
    /// <see cref="CompletePullRequestAsync"/> stays init's completion. 2026-11-25-9992i: <paramref name="style"/> squashes a
    /// merge into a series branch; <see cref="MergeStyle.HostDefault"/> keeps the repository's own method.
    /// </summary>
    Task<PullRequestCompletion> MergePullRequestAsync(
        string prUrl, BranchName sourceBranch, BranchProtection protection, MergeStyle style, CancellationToken cancellationToken);
}
