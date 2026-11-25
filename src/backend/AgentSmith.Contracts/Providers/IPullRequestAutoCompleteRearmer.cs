namespace AgentSmith.Contracts.Providers;

/// <summary>
/// 2026-11-24-629ab: arms a pull request's auto-complete again after its host dropped it at a merge conflict — the host's own
/// completion, so it never goes past a branch policy, and no approval vote is cast. Implemented by the Azure Repos provider,
/// reached by casting. 2026-11-25-7a92c: and by GitLab, which cancels auto-merge when new commits arrive (a refresh's merge of
/// the base among them). GitHub keeps auto-merge through agent-smith's own pushes and conflicts — it drops it only for a push
/// without write access or a base switch — so it records no loss and has nothing to arm again.
/// </summary>
public interface IPullRequestAutoCompleteRearmer
{
    /// <summary>Armed when the host took it (merged when it completed there and then), refused with the host's reason.</summary>
    Task<PullRequestCompletion> RearmAutoCompleteAsync(string prUrl, CancellationToken cancellationToken);
}
