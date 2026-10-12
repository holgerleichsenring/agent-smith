namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Reads where a pull request comes from, for a host whose comment delivery does not say — GitHub's
/// issue_comment carries no head. Implemented by the source providers, reached by casting.
/// </summary>
public interface IPullRequestHeadReader
{
    Task<PullRequestHead> HeadAsync(string prNumber, CancellationToken cancellationToken);
}
