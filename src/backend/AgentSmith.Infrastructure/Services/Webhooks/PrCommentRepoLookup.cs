using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Services.Webhooks;

/// <summary>Reads the server's configuration and matches the repository URL against it.</summary>
public sealed class PrCommentRepoLookup(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    IConfiguredRepoFinder repoFinder) : IPrCommentRepoLookup
{
    public ConfiguredRepo? Find(string repositoryUrl) =>
        repoFinder.Find(configLoader.LoadConfig(serverContext.ConfigPath), repositoryUrl);
}
