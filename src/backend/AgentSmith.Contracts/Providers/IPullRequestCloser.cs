namespace AgentSmith.Contracts.Providers;

/// <summary>
/// 2026-10-15-114fa: closes an open pull request without merging it — reached by casting a repository's source
/// provider, like the review readers, so a provider that cannot close one simply does not implement it.
/// </summary>
public interface IPullRequestCloser
{
    /// <summary>Closes the pull request at the URL; throws when the host refuses or cannot be reached.</summary>
    Task CloseAsync(string prUrl, CancellationToken cancellationToken);
}
