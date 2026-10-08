using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Configuration;

/// <summary>
/// 2026-10-08-101b: a repository declared by projects of a polling tracker entry and of a webhook one.
/// Its pull-request deliveries are refused for all of them (any polling owner gates the repository),
/// so the webhook projects get pull-request events only through the PR sweep — said at startup.
/// </summary>
public static class TriggerModeFindings
{
    public static IEnumerable<StartupFinding> Findings(AgentSmithConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.Projects.Values
            .SelectMany(p => p.Repos.Where(r => r.Url is not null).Select(r => (Key: Key(r.Url!), Project: p)))
            .GroupBy(x => x.Key)
            .Where(g => g.Select(x => Polls(config, x.Project)).Distinct().Count() > 1)
            .Select(g => new StartupFinding(StartupSubsystems.Configuration, StartupFindingSeverity.Advisory,
                $"Repository {g.Key} is declared by projects of a polling tracker ({Names(config, g, true)}) and of a webhook one "
                + $"({Names(config, g, false)}); its pull-request webhooks start nothing for any of them."));
    }

    private static bool Polls(AgentSmithConfig config, ResolvedProject project) =>
        (config.Trackers.TryGetValue(project.Tracker.Name, out var entry) ? entry : project.Tracker).Polling.Enabled;

    private static string Names(AgentSmithConfig config, IEnumerable<(string Key, ResolvedProject Project)> owners, bool polling) =>
        string.Join(", ", owners.Where(x => Polls(config, x.Project) == polling).Select(x => x.Project.Name).Distinct());

    private static string Key(string url) =>
        (Uri.TryCreate(url, UriKind.Absolute, out var uri) ? $"{uri.Host}{uri.AbsolutePath}" : url).TrimEnd('/')
            .Replace(".git", string.Empty, StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
}
