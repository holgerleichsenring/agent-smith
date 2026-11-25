using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// 2026-11-25-0873: while PoCs are on, a target whose source repository's hoster <c>pocs.machine</c> does not name is advisory:
/// its install, source and GitOps commits and builds go out with the repository's own token (a poc- project's own machine
/// block, when an operator gave one, still wins at the act). An unusable block is refused with the rest of <c>pocs:</c>.
/// </summary>
public static class PocMachineFindings
{
    public static IEnumerable<StartupFinding> Missing(AgentSmithConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (!config.Pocs.Enabled) yield break;
        foreach (var target in config.Pocs.Targets)
            if (HosterOf(config, target.SourceRepo) is { } hoster and not RepoType.Local && config.Pocs.Machine?.For(hoster) is null)
                yield return new StartupFinding(StartupSubsystems.Configuration, StartupFindingSeverity.Advisory,
                    $"PoC target '{target.Name}' has no machine identity for its source repository '{target.SourceRepo}' ({hoster}): its "
                    + "pipeline, commits and builds go out with the repository's own token, as whoever owns it. "
                    + $"Set pocs.machine.{MachineIdentityProblems.Key(hoster)} in the PoC settings.",
                    Field: "pocs.machine");
    }

    // A catalog name, or connection/name of a configured connection, as PocSourceRepos resolves it.
    private static RepoType? HosterOf(AgentSmithConfig config, string reference)
    {
        if (config.Repos.FirstOrDefault(r => ConfigNames.Comparer.Equals(r.Key, reference)).Value is { } repo) return repo.Type;
        var slash = reference.IndexOf('/');
        return slash > 0 && config.Connections.FirstOrDefault(c => ConfigNames.Comparer.Equals(c.Key, reference[..slash])).Value is { } connection
            ? connection.Type : null;
    }
}
