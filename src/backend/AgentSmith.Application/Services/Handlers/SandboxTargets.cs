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
    /// Given a RepoConnection, returns the (sandbox-key, ISandbox) pairs that
    /// serve that repo. p0249: resolves from the AUTHORITATIVE key→repo map
    /// (ContextKeys.SandboxRepos, published by the coordinator that knows the
    /// repo for each key). Falls back to decoding the SandboxKeyComposer scheme
    /// when the map is absent — and the fallback now covers ALL of its key forms:
    ///   single-repo (Repos.Count == 1)        → every sandbox serves this repo
    ///   multi-repo single-group  (key == repo)             → exact
    ///   multi-repo monorepo      (key starts with repo + "/")  → context prefix
    ///   multi-repo multi-group   (key starts with repo + "-") → toolchain suffix
    /// The last form was MISSING, so a multi-group repo's source change was
    /// silently dropped at commit time (p0249 root cause).
    /// </summary>
    public static IReadOnlyList<KeyValuePair<string, ISandbox>> SandboxesForRepo(
        PipelineContext pipeline, RepoConnection repo)
    {
        if (!pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(
                ContextKeys.Sandboxes, out var sandboxes) || sandboxes is null)
            return [];
        pipeline.TryGet<IReadOnlyDictionary<string, string>>(
            ContextKeys.SandboxRepos, out var owners);
        var repoCount = pipeline.TryGet<IReadOnlyList<RepoConnection>>(
            ContextKeys.Repos, out var repos) && repos is not null ? repos.Count : 1;
        return sandboxes
            .Where(kv => KeyBelongsToRepo(kv.Key, repo.Name, repoCount > 1, owners))
            .ToList();
    }

    /// <summary>
    /// p0322b: the ONE key→repo ownership test. Authoritative when the
    /// coordinator's ContextKeys.SandboxRepos map is present (it records the
    /// owner the moment each key is composed). The string fallback decodes the
    /// SandboxKeyComposer scheme and exists ONLY for older contexts without the
    /// map — string matching is the bug class that dropped multi-group repos
    /// twice (commit targeting in p0249, re-init projection in p0322b).
    /// </summary>
    public static bool KeyBelongsToRepo(
        string sandboxKey, string repoName, bool multiRepo,
        IReadOnlyDictionary<string, string>? owners)
    {
        if (owners is not null)
            return owners.TryGetValue(sandboxKey, out var owner) && owner == repoName;
        return !multiRepo
            || sandboxKey == repoName
            || sandboxKey.StartsWith(repoName + "/", StringComparison.Ordinal)
            || sandboxKey.StartsWith(repoName + "-", StringComparison.Ordinal);
    }
}
