using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// p0281a: converts raw <c>connections:</c> YAML entries into <see cref="ResolvedConnection"/>
/// records keyed by catalog name. Type binding happens at YAML deserialize time via the
/// snake_case enum convention.
/// p0515: the catalog is keyed by <see cref="ConfigNames.Comparer"/> — RepoGlobExpander
/// already grouped connection references case-insensitively before looking them up here.
/// </summary>
public sealed class ConnectionCatalogBuilder
{
    private readonly CatalogKeyCollisions _collisions = new();

    public Dictionary<string, ResolvedConnection> Build(
        IReadOnlyDictionary<string, RawConnectionEntry> raw, List<StartupFinding> findings)
    {
        var dropped = _collisions.Detect("connections", raw.Keys, findings);
        var result = new Dictionary<string, ResolvedConnection>(raw.Count, ConfigNames.Comparer);

        foreach (var (name, entry) in raw)
        {
            if (dropped.Contains(name)) continue;
            result[name] = new ResolvedConnection
            {
                Name = name,
                Type = entry.Type,
                Organization = entry.Organization,
                Project = entry.Project,
                Owner = entry.Owner,
                Group = entry.Group,
                Host = entry.Host,
                Auth = entry.Auth,
                DefaultBranch = entry.DefaultBranch,
            };
        }

        return result;
    }
}
