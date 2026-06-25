using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// p0281a: converts raw <c>connections:</c> YAML entries into <see cref="ResolvedConnection"/>
/// records keyed by catalog name. Type binding happens at YAML deserialize time via the
/// snake_case enum convention.
/// </summary>
public sealed class ConnectionCatalogBuilder
{
    public Dictionary<string, ResolvedConnection> Build(
        IReadOnlyDictionary<string, RawConnectionEntry> raw)
    {
        var result = new Dictionary<string, ResolvedConnection>(raw.Count);

        foreach (var (name, entry) in raw)
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

        return result;
    }
}
