using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// Converts raw `repos:` YAML entries into <see cref="RepoConnection"/>
/// records keyed by catalog name. Type-parse failures are recorded into the
/// passed-in errors list — the builder never throws.
/// </summary>
public sealed class RepoCatalogBuilder
{
    public Dictionary<string, RepoConnection> Build(
        IReadOnlyDictionary<string, RawRepoEntry> raw, List<string> errors)
    {
        var result = new Dictionary<string, RepoConnection>(raw.Count);

        foreach (var (name, entry) in raw)
        {
            var connection = TryBuild(name, entry, errors);
            if (connection is not null) result[name] = connection;
        }
        return result;
    }

    private static RepoConnection? TryBuild(string name, RawRepoEntry entry, List<string> errors)
    {
        if (!Enum.TryParse<RepoType>(entry.Type, ignoreCase: true, out var type))
        {
            errors.Add(
                $"Repo '{name}': unknown type '{entry.Type}' " +
                "(expected GitHub|GitLab|AzureDevOps|Local)");
            return null;
        }

        return new RepoConnection
        {
            Name = name,
            Type = type,
            Url = entry.Url,
            Path = entry.Path,
            Organization = entry.Organization,
            Project = entry.Project,
            Auth = entry.Auth,
            DefaultBranch = entry.DefaultBranch,
        };
    }
}
