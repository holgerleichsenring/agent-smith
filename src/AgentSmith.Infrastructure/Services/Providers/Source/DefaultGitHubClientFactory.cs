using Octokit;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// Production GitHubClient factory: ProductHeaderValue("AgentSmith") + bearer
/// token credentials. Stateless — registered as a DI singleton.
/// </summary>
public sealed class DefaultGitHubClientFactory : IGitHubClientFactory
{
    public IGitHubClient Create(string token)
    {
        var client = new GitHubClient(new ProductHeaderValue("AgentSmith"));
        client.Credentials = new Credentials(token);
        return client;
    }
}
