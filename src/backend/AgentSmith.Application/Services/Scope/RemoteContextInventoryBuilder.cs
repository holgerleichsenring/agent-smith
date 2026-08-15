using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Scope;

/// <summary>
/// p0331: the pre-checkout remote context inventory — one ResolveAllAsync pass
/// per repo over ISourceProvider, cached at ContextKeys.RemoteContextInventory so
/// PipelineSandboxCoordinator never re-reads the same context.yamls remotely.
/// <para>
/// p0413: split out of ScopeReposHandler — building the inventory is a
/// provisioning concern the scoping decision merely happens to need first.
/// </para>
/// </summary>
public sealed class RemoteContextInventoryBuilder(
    ISandboxLanguageResolver languageResolver,
    ILogger<RemoteContextInventoryBuilder> logger)
{
    /// <summary>
    /// The inventory covers ALL repos as seen BEFORE narrowing, so a mid-run
    /// ensure_repo_sandbox escalation to a descoped repo also hits the cache.
    /// p0261 `--context NAME` pins every repo to one named context — the
    /// coordinator resolves via ResolveContextAsync then, so no inventory is
    /// cached (it would not be consumed).
    /// </summary>
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>> BuildAsync(
        PipelineContext pipeline, IReadOnlyList<RepoConnection> repos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(repos);
        var inventory = new Dictionary<string, IReadOnlyList<RemoteContextDiscovery>>(StringComparer.Ordinal);
        foreach (var repo in repos)
            inventory[repo.Name ?? string.Empty] = await languageResolver.ResolveAllAsync(repo, ct);

        var contextOverride = pipeline.TryGet<string>(ContextKeys.SourceContext, out var ctx)
            && !string.IsNullOrWhiteSpace(ctx);
        if (!contextOverride)
            pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
                ContextKeys.RemoteContextInventory, inventory);
        logger.LogInformation(
            "Remote context inventory: {Repos} repo(s), {Contexts} context(s){Cached}",
            inventory.Count, inventory.Values.Sum(v => v.Count),
            contextOverride ? " (not cached — --context override active)" : string.Empty);
        return inventory;
    }
}
