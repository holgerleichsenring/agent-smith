using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Finds the configured repository a code host's event names by its URL. The comparison is
/// host + path, so a payload's clone URL matches the operator's configured web URL.
/// </summary>
public interface IConfiguredRepoFinder
{
    ConfiguredRepo? Find(AgentSmithConfig config, string repoUrl);
}
