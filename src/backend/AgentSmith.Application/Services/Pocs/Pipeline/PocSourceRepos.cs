using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Pocs.Pipeline;

/// <summary>2026-11-23-933dd: a target's source_repo as the repository it names — a repos-catalog name, or
/// connection/name of a configured connection, built as a project's exact connection reference is.</summary>
public sealed class PocSourceRepos(IConnectionRepoUrlBuilder urls)
{
    public (RepoConnection? Repo, string? Problem) Resolve(AgentSmithConfig config, PocTargetConfig target)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(target);
        var reference = target.SourceRepo;
        // 2026-11-25-0873: the repository acts under pocs.machine.
        if (config.Repos.FirstOrDefault(r => ConfigNames.Comparer.Equals(r.Key, reference)).Value is { } catalogued)
            return (PocMachine.Stamp(config.Pocs, catalogued), null);
        var slash = reference.IndexOf('/');
        if (slash > 0 && config.Connections.FirstOrDefault(c => ConfigNames.Comparer.Equals(c.Key, reference[..slash])).Value is { } connection)
            return (PocMachine.Stamp(config.Pocs, urls.Build(connection, reference[(slash + 1)..], null)), null);
        return (null, $"pocs.targets[].source_repo of target '{target.Name}' names '{reference}', which is neither a repos-catalog "
            + "name nor connection/name of a configured connection");
    }
}
