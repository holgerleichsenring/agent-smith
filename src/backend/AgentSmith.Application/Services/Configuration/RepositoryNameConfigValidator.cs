using AgentSmith.Contracts.Memory;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Configuration;

/// <summary>
/// 2026-11-10-fcd7a: a repository name may not start with '@'. A memory tool and a citation name a project's own
/// store '@project', so a repository named like it would be indistinguishable from it.
/// </summary>
public static class RepositoryNameConfigValidator
{
    public static IEnumerable<StartupFinding> Findings(AgentSmithConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var named = config.Repos.Keys
            .Concat(config.Projects.Values.SelectMany(p => p.Repos.Select(r => r.Name)))
            .Where(name => name.StartsWith('@')).Distinct(StringComparer.Ordinal);
        return named.Select(name => new StartupFinding(
            StartupSubsystems.Configuration, StartupFindingSeverity.Blocking,
            $"Repository '{name}': a repository name may not start with '@' — '{ProjectMemorySource.Label}' names a " +
            "project's own memory store.", Field: "repos"));
    }
}
