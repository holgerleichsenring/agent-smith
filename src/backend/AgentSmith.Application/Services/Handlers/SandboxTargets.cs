using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Reads ContextKeys.Sandboxes + ContextKeys.SandboxDiscoveries from the
/// pipeline (both seeded by PipelineSandboxCoordinator after p0161a). Returns
/// false when either is missing — handlers turn that into a Skip/Fail per
/// their semantics.
/// </summary>
internal static class SandboxTargets
{
    public static bool TryResolve(
        PipelineContext pipeline,
        out IReadOnlyDictionary<string, ISandbox> sandboxes,
        out IReadOnlyDictionary<string, RemoteContextDiscovery> discoveries)
    {
        sandboxes = null!;
        discoveries = null!;
        if (!pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(
                ContextKeys.Sandboxes, out var s) || s is null || s.Count == 0)
            return false;
        if (!pipeline.TryGet<IReadOnlyDictionary<string, RemoteContextDiscovery>>(
                ContextKeys.SandboxDiscoveries, out var d) || d is null)
            return false;
        sandboxes = s;
        discoveries = d;
        return true;
    }

    /// <summary>
    /// p0161a/b: given a RepoConnection, returns the (sandbox-key, ISandbox)
    /// pairs that serve that repo's discovered contexts. Decodes the
    /// SandboxKeyComposer scheme:
    ///   single-repo (Repos.Count == 1)  → every sandbox belongs to this repo
    ///   multi-repo single-context (key == repo.Name)     → exact match
    ///   multi-repo monorepo (key starts with repo.Name + "/")  → prefix match
    /// </summary>
    public static IReadOnlyList<KeyValuePair<string, ISandbox>> SandboxesForRepo(
        PipelineContext pipeline, RepoConnection repo)
    {
        if (!pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(
                ContextKeys.Sandboxes, out var sandboxes) || sandboxes is null)
            return [];
        var repoCount = pipeline.TryGet<IReadOnlyList<RepoConnection>>(
            ContextKeys.Repos, out var repos) && repos is not null ? repos.Count : 1;

        // Single-repo: every sandbox serves this repo.
        if (repoCount <= 1)
            return sandboxes.ToList();

        // Multi-repo: exact match (single-context) or "<repo>/..." prefix (monorepo).
        var prefix = repo.Name + "/";
        return sandboxes
            .Where(kv => kv.Key == repo.Name || kv.Key.StartsWith(prefix, StringComparison.Ordinal))
            .ToList();
    }
}
