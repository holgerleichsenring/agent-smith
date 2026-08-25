using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// Converts raw `repos:` YAML entries into <see cref="RepoConnection"/>
/// records keyed by catalog name. Type binding happens at YAML deserialize
/// time via the snake_case enum convention; unknown values fail there.
/// p0515: the catalog is keyed by <see cref="ConfigNames.Comparer"/>, and a pair of
/// names that differ only in case is dropped and named rather than collapsed.
/// </summary>
public sealed class RepoCatalogBuilder
{
    private readonly CatalogKeyCollisions _collisions = new();

    public Dictionary<string, RepoConnection> Build(
        IReadOnlyDictionary<string, RawRepoEntry> raw, List<StartupFinding> findings)
    {
        var dropped = _collisions.Detect("repos", raw.Keys, findings);
        var result = new Dictionary<string, RepoConnection>(raw.Count, ConfigNames.Comparer);

        foreach (var (name, entry) in raw)
        {
            if (dropped.Contains(name)) continue;
            result[name] = new RepoConnection
            {
                Name = name,
                Type = entry.Type,
                Url = entry.Url,
                Path = entry.Path,
                Organization = entry.Organization,
                Project = entry.Project,
                Auth = entry.Auth,
                DefaultBranch = entry.DefaultBranch,
            };
        }

        return result;
    }
}
