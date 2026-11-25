using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Preflight.Checks;

/// <summary>
/// 2026-11-25-0873: the repositories a preflight check reaches, as they act. A project's repos come stamped with its machine
/// identity, so they are probed with the token their pushes and pull requests use; a catalog repository is probed with its own
/// auth too, which the reads under it (polling, sweeps) use. One remote reached under several identities is probed once each.
/// </summary>
public static class PreflightRepos
{
    public static IReadOnlyList<RepoConnection> Acting(AgentSmithConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return [.. config.Projects.Values.SelectMany(p => p.Repos).Concat(config.Repos.Values).DistinctBy(Identity, StringComparer.OrdinalIgnoreCase)];
    }

    // What is actually reached: the remote and the identity reaching it — the machine's secret, or the repo's own auth.
    private static string Identity(RepoConnection repo) =>
        $"{repo.Type}|{repo.Url ?? repo.Path ?? repo.Name}|{repo.Machine?.Name}|{repo.Machine?.Auth ?? repo.Auth}|{repo.Machine?.InstallationId}";
}
