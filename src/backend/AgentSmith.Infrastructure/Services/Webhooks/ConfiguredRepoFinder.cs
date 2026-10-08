using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Services.Webhooks;

/// <summary>
/// Matches an event's repository URL against every configured project's repos, in config
/// order. An unconfigured repository matches nothing, so an event on it triggers nothing.
/// </summary>
public sealed class ConfiguredRepoFinder : IConfiguredRepoFinder
{
    public ConfiguredRepo? Find(AgentSmithConfig config, string repoUrl) => FindAll(config, repoUrl).FirstOrDefault();

    // 2026-10-08-e8b9c: equality, not containment — `org/app` is a prefix of `org/app-api`, and a
    // first match by substring routed one repository's events to another's project.
    public IReadOnlyList<ConfiguredRepo> FindAll(AgentSmithConfig config, string repoUrl)
    {
        var candidate = Normalize(repoUrl);
        var found = new List<ConfiguredRepo>();
        foreach (var (projectName, project) in config.Projects)
            foreach (var repo in project.Repos)
                if (repo.Url is not null && string.Equals(candidate, Normalize(repo.Url), StringComparison.Ordinal))
                    found.Add(new ConfiguredRepo(projectName, project, repo));
        return found;
    }

    /// <summary>Host + path, lowercased, no scheme/userinfo/.git suffix — so a
    /// payload clone_url ("https://user@host/org/repo.git") matches the
    /// operator's configured web URL ("https://host/org/repo").</summary>
    private static string Normalize(string url)
    {
        var normalized = Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? $"{uri.Host}{uri.AbsolutePath}"
            : url;
        normalized = normalized.TrimEnd('/');
        if (normalized.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[..^4];
        return normalized.ToLowerInvariant();
    }
}
