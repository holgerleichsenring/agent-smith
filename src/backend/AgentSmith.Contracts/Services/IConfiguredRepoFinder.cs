using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Finds the configured repository a code host's event names by its URL. The comparison is
/// host + path compared for EQUALITY (2026-10-08-e8b9c — a substring made `app` match `app-api`), so a payload's clone URL matches the operator's configured web URL.
/// </summary>
public interface IConfiguredRepoFinder
{
    ConfiguredRepo? Find(AgentSmithConfig config, string repoUrl);

    /// <summary>2026-10-08-e8b9c: every configured repo, in config order, whose host + path EQUALS
    /// the event's — several projects may configure one repository.</summary>
    IReadOnlyList<ConfiguredRepo> FindAll(AgentSmithConfig config, string repoUrl);
}
