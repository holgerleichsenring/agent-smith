using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// Converts raw `repos:` YAML entries into <see cref="RepoConnection"/>
/// records keyed by catalog name. Type binding happens at YAML deserialize
/// time via the snake_case enum convention; unknown values fail there.
/// </summary>
public sealed class RepoCatalogBuilder
{
    public Dictionary<string, RepoConnection> Build(
        IReadOnlyDictionary<string, RawRepoEntry> raw, List<string> _)
    {
        var result = new Dictionary<string, RepoConnection>(raw.Count);

        foreach (var (name, entry) in raw)
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

        return result;
    }
}
