namespace AgentSmith.Contracts.Providers;

/// <summary>
/// 2026-11-24-629ab: arms a pull request's auto-complete again after its host dropped it at a merge conflict — the host's own
/// completion, so it never goes past a branch policy, and no approval vote is cast. Implemented by the Azure Repos provider,
/// reached by casting; GitHub and GitLab say nothing of auto-merge in their state reads, so no drop is ever recorded there.
/// </summary>
public interface IPullRequestAutoCompleteRearmer
{
    /// <summary>Armed when the host took it (merged when it completed there and then), refused with the host's reason.</summary>
    Task<PullRequestCompletion> RearmAutoCompleteAsync(string prUrl, CancellationToken cancellationToken);
}
