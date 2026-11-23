using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Configuration;

/// <summary>
/// 2026-11-23-933da: the one home of the pocs block's references to other entities — each target's source_repo names a
/// catalog repo or a connection, its registry's pull_auth names a catalog secret (2026-11-23-933de), and project_agent
/// and project_tracker name configured ones. A settings singleton carries no reference edges, so a dangling name is found here at the load. Blocking while PoCs are on; advisory
/// while off, because the form may be filled in before the switch is.
/// </summary>
public static class PocTargetReferences
{
    public static IEnumerable<StartupFinding> Findings(AgentSmithConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var severity = config.Pocs.Enabled ? StartupFindingSeverity.Blocking : StartupFindingSeverity.Advisory;
        return Problems(config).Select(problem => new StartupFinding(
            StartupSubsystems.Configuration, severity, problem, Field: "pocs"));
    }

    private static IEnumerable<string> Problems(AgentSmithConfig config)
    {
        var pocs = config.Pocs;
        if (Dangling(pocs.ProjectAgent, config.Agents.Keys))
            yield return $"pocs.project_agent names '{pocs.ProjectAgent}', which is not a configured agent";
        if (Dangling(pocs.ProjectTracker, config.Trackers.Keys))
            yield return $"pocs.project_tracker names '{pocs.ProjectTracker}', which is not a configured tracker";
        foreach (var target in pocs.Targets.Where(t => !string.IsNullOrWhiteSpace(t.SourceRepo) && !Resolves(t.SourceRepo, config)))
            yield return $"pocs.targets[].source_repo of target '{target.Name}' names '{target.SourceRepo}', which is neither "
                + "a repos-catalog name nor connection/name of a configured connection";
        foreach (var target in pocs.Targets.Where(t => t.Registry is { PullAuth.Length: > 0 } r && Dangling(r.PullAuth, config.Secrets.Keys)))
            yield return $"{PocRegistryConfig.Key}.pull_auth of target '{target.Name}' names '{target.Registry!.PullAuth}', "
                + "which is not a secret of the secrets catalog; add the pull-only token there";
    }

    private static bool Dangling(string? name, IEnumerable<string> known) =>
        !string.IsNullOrWhiteSpace(name) && !known.Contains(name, ConfigNames.Comparer);

    private static bool Resolves(string sourceRepo, AgentSmithConfig config)
    {
        var slash = sourceRepo.IndexOf('/');
        return slash > 0
            ? config.Connections.Keys.Contains(sourceRepo[..slash], ConfigNames.Comparer) && slash < sourceRepo.Length - 1
            : config.Repos.Keys.Contains(sourceRepo, ConfigNames.Comparer);
    }
}
