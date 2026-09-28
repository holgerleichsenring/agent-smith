using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Services.Webhooks;

/// <summary>
/// Matches an event's repository URL against every configured project's repos, in config
/// order. An unconfigured repository matches nothing, so an event on it triggers nothing.
/// </summary>
public sealed class ConfiguredRepoFinder : IConfiguredRepoFinder
{
    public ConfiguredRepo? Find(AgentSmithConfig config, string repoUrl)
    {
        var candidate = Normalize(repoUrl);
        foreach (var (projectName, project) in config.Projects)
            foreach (var repo in project.Repos)
                if (repo.Url is not null && candidate.Contains(Normalize(repo.Url), StringComparison.Ordinal))
                    return new ConfiguredRepo(projectName, project, repo);
        return null;
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
