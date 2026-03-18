namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Posts comments on pull requests / merge requests.
/// Implemented by source providers that support PR commenting (GitHub, GitLab, AzDO).
/// </summary>
public interface IPrCommentProvider
{
    Task PostCommentAsync(string prIdentifier, string markdown, CancellationToken cancellationToken = default);
}
