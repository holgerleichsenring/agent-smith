using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-11-25-9992b: which toolchain groups a run's repositories need — every repository's contexts, grouped by the
/// image they resolve to (p0336c), each group sized to its members' largest resource envelope. Moved out of the
/// coordinator, which now only decides what the workspace holds.
/// </summary>
public sealed class WorkspaceGroupPlanner(ISandboxLanguageResolver languages, SandboxSpecBuilder specs)
{
    public async Task<IReadOnlyList<WorkspaceGroup>> PlanAsync(
        ResolvedProject project, PipelineContext context, string? pipelineName,
        IEnumerable<RepoConnection> repos, CancellationToken cancellationToken)
    {
        var groups = new List<WorkspaceGroup>();
        foreach (var repo in repos)
        {
            var discoveries = await DiscoverAsync(repo, context, cancellationToken);
            var specced = discoveries
                .Select(d => (Discovery: d, Spec: specs.Build(project, d.Language, pipelineName, d.ToolchainImage, d.Resources)))
                .ToList();
            var byImage = ToolchainGroupOrder.RootFirst(
                specced.GroupBy(x => x.Spec.ToolchainImage, StringComparer.Ordinal), x => x.Discovery.Workdir).ToList();
            groups.AddRange(byImage.Select(group => group.ToList()).Select(members => new WorkspaceGroup(
                repo, [.. members.Select(m => m.Discovery)],
                members[0].Spec with { Resources = ResourceEnvelope.Max(members.Select(m => m.Spec.Resources)) },
                byImage.Count)));
        }
        return groups;
    }

    // p0261: `--context NAME` pins every repo to one named context. p0331: ScopeRepos already read every repo's
    // context.yamls on the branch the run works on (2026-10-12-6bfaa); the inventory is consumed, not re-read.
    private async Task<IReadOnlyList<RemoteContextDiscovery>> DiscoverAsync(
        RepoConnection repo, PipelineContext context, CancellationToken ct)
    {
        var readBranches = ContextReadBranches.For(context, repo.Name); // 2026-11-23-31dca: the repo's own rung
        if (context.TryGet<string>(ContextKeys.SourceContext, out var pinned) && !string.IsNullOrWhiteSpace(pinned))
            return await languages.ResolveContextAsync(repo, pinned, readBranches, ct);
        return context.TryGet<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
                   ContextKeys.RemoteContextInventory, out var inventory)
               && inventory is not null && inventory.TryGetValue(repo.Name ?? string.Empty, out var cached)
            ? cached
            : await languages.ResolveAllAsync(repo, readBranches, ct);
    }

    /// <summary>One toolchain per image across all groups, each at the largest envelope any group of it asks for.</summary>
    public IReadOnlyList<WorkspaceToolchain> Toolchains(IEnumerable<WorkspaceGroup> groups) =>
        [.. groups.GroupBy(g => g.Image, StringComparer.Ordinal)
            .Select(g => new WorkspaceToolchain(g.Key, ResourceEnvelope.Max(g.Select(x => x.Spec.Resources))))];
}
