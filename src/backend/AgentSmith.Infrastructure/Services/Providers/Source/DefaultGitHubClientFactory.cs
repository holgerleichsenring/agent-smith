using AgentSmith.Infrastructure.Models;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// Production GitHubClient factory: ProductHeaderValue("AgentSmith") + bearer
/// token credentials on the address's REST base. Stateless — registered as a DI singleton.
/// </summary>
public sealed class DefaultGitHubClientFactory : IGitHubClientFactory
{
    public IGitHubClient Create(GitHubRepoAddress address, string token)
    {
        var client = new GitHubClient(new ProductHeaderValue("AgentSmith"), address.Api.Rest);
        client.Credentials = new Credentials(token);
        return client;
    }
}
