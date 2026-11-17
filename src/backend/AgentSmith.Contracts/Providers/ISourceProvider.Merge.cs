using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Providers;

public partial interface ISourceProvider
{
    /// <summary>
    /// 2026-11-17-023fa: merges a code run's pull request in the mode the project chose. <see cref="BranchProtection.Bypass"/>
    /// merges now, past branch policies, protection and approval rules, as far as the token's identity may; a host that
    /// still refuses answers a refusal that names the policy, the permission to grant and the
    /// <c>respect_branch_protection</c> key. <see cref="BranchProtection.Respect"/> arms the host's auto-complete or
    /// auto-merge, so its rules decide when. Never throws; a local repository fast-forwards its default branch either way.
    /// <see cref="CompletePullRequestAsync"/> stays init's completion.
    /// </summary>
    Task<PullRequestCompletion> MergePullRequestAsync(
        string prUrl, BranchName sourceBranch, BranchProtection protection, CancellationToken cancellationToken);
}
