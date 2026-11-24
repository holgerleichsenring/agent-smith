namespace AgentSmith.Contracts.Reviews;

/// <summary>
/// 2026-11-24-f9c5d: starts the pull-request CI a repository runs for its default branch on a pull request into another
/// branch — a series branch, which hosts often leave without the required builds of a protected branch. Never throws for a
/// host's refusal: that is an <see cref="CiTriggerOutcome.Impossible"/> answer with the host's words.
/// </summary>
public interface IPullRequestCiTrigger
{
    /// <param name="defaultBranch">The repository's configured default branch; null asks the host.</param>
    Task<CiTrigger> StartAsync(string prUrl, string? defaultBranch, CancellationToken cancellationToken);
}
