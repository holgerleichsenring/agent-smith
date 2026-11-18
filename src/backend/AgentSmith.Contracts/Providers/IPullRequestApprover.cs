namespace AgentSmith.Contracts.Providers;

/// <summary>
/// 2026-11-17-3435b: approves a pull request when its host requires an approving review before it merges, as the
/// identity the provider authenticates as. Implemented by the GitHub, GitLab and Azure DevOps providers, reached by
/// casting; the caller picks the identity by the repo connection it creates the provider from.
/// </summary>
public interface IPullRequestApprover
{
    Task<PullRequestApproval> ApproveIfRequiredAsync(string prUrl, CancellationToken cancellationToken);
}
