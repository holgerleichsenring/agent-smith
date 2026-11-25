using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// 2026-11-25-9992h: a project's repos stamped with their hoster's machine identity as the project resolves. A block that
/// cannot act is a blocking finding and drops the project, as any other unusable value does; a hoster with no identity is
/// advisory, recorded by the materializer (<see cref="ProjectMachineFindings.Missing(IEnumerable{ResolvedProject})"/>).
/// </summary>
public static class ProjectMachineStamp
{
    /// <summary>The stamped repos; null when the block cannot act, its findings added to <paramref name="findings"/>.</summary>
    public static IReadOnlyList<RepoConnection>? Apply(
        string project, MachineIdentityConfig? machine, IReadOnlyList<RepoConnection> repos, List<StartupFinding> findings)
    {
        var unusable = ProjectMachineFindings.Unusable(project, machine).ToList();
        findings.AddRange(unusable);
        if (unusable.Count > 0) return null;
        return [.. repos.Select(r => r.Type == RepoType.Local ? r : r with { Machine = machine?.IdentityFor(r.Type) })];
    }
}
