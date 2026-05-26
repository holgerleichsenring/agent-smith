using Octokit;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// Constructs an authenticated Octokit client. Returns the <see cref="IGitHubClient"/>
/// interface (not the concrete <see cref="GitHubClient"/>) so tests can pass a Moq
/// stub through this seam — previously the provider hard-newed the client inside
/// CreateGitHubClient() and `IGitHubClient` was inaccessible from tests.
/// </summary>
public interface IGitHubClientFactory
{
    IGitHubClient Create(string token);
}
