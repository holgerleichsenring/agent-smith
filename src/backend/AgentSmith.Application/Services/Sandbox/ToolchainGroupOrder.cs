using AgentSmith.Application.Services.Structure;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// Orders one repository's toolchain-image groups. The first group's sandbox represents the
/// repository: its alias, the default run_command target and its capability line. A group
/// holding a root-workdir context comes first, because the root context's files are
/// everywhere; otherwise the provider's listing order stands (the sort is stable).
/// </summary>
public static class ToolchainGroupOrder
{
    public static IEnumerable<IGrouping<TKey, TMember>> RootFirst<TKey, TMember>(
        IEnumerable<IGrouping<TKey, TMember>> groups, Func<TMember, string?> workdir) =>
        groups.OrderBy(g => g.Any(m => StructureWorkdir.Normalize(workdir(m)) == string.Empty) ? 0 : 1);
}
