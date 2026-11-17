using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Scope;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Preflight.Run;

/// <summary>
/// 2026-11-16-f3fba: the contexts this run's sandboxes will be started for, read before any sandbox
/// exists — the remote inventory restricted to <see cref="ContextKeys.Repos"/> and
/// <see cref="ContextKeys.ScopedContexts"/>, by the coordinator's own rule. Where ScopeRepos built no
/// inventory (pr-review, security-scan) it is built here; a <c>--context NAME</c> run reads the one
/// named context per repository, as the coordinator does.
/// </summary>
public sealed class RunContextInventory(
    RemoteContextInventoryBuilder builder, ISandboxLanguageResolver resolver)
{
    /// <summary>Repository name → the contexts its sandboxes will carry.</summary>
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>> ReadAsync(
        PipelineContext pipeline, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var repos = pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var r) && r is not null
            ? r : [];
        if (pipeline.TryGet<string>(ContextKeys.SourceContext, out var pinned) && !string.IsNullOrWhiteSpace(pinned))
            return await PinnedAsync(pipeline, repos, pinned, ct);
        var inventory = pipeline.TryGet<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
            ContextKeys.RemoteContextInventory, out var inv) && inv is not null
            ? inv : await builder.BuildAsync(pipeline, repos, ct);
        var scoped = pipeline.TryGet<IReadOnlyDictionary<string, IReadOnlyList<string>>>(
            ContextKeys.ScopedContexts, out var sc) ? sc : null;
        return repos
            .Where(repo => inventory.ContainsKey(repo.Name ?? string.Empty))
            .ToDictionary(repo => repo.Name ?? string.Empty,
                repo => ScopedContextFilter.Apply(inventory[repo.Name ?? string.Empty], repo.Name, scoped),
                StringComparer.Ordinal);
    }

    private async Task<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>> PinnedAsync(
        PipelineContext pipeline, IReadOnlyList<RepoConnection> repos, string context, CancellationToken ct)
    {
        var branches = ContextReadBranches.For(pipeline);
        var pinned = new Dictionary<string, IReadOnlyList<RemoteContextDiscovery>>(StringComparer.Ordinal);
        foreach (var repo in repos)
            pinned[repo.Name ?? string.Empty] = await resolver.ResolveContextAsync(repo, context, branches, ct);
        return pinned;
    }
}
