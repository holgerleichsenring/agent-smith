using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Persistence;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Runs the agentic ProjectAnalyzer per discovered context (p0161a).
/// Iterates ContextKeys.Sandboxes keys; per key analyzes the sub-tree at
/// `/work/{discovery.Workdir}` (which is /work for single-stack repos and a
/// sub-folder for monorepo contexts). Populates ContextKeys.RepoProjectMaps and
/// ContextKeys.RepoCodeMaps (keyed by sandbox key) — the only analysis surface;
/// p0384 removed the singular first-key collapse so downstream prompts see EVERY
/// scoped repo. Cache I/O goes through p0182's IProjectMapStore — Redis on the
/// server, disk on the CLI.
/// </summary>
public sealed class AnalyzeProjectHandler(
    IProjectAnalyzer analyzer,
    ISandboxFileReaderFactory readerFactory,
    IProjectMapStore mapStore,
    SandboxGitOperations gitOps,
    IRunArtifactStore artifactStore,
    ProjectMapCacheKey projectMapCacheKey,
    SandboxTargets sandboxTargets,
    ILogger<AnalyzeProjectHandler> logger) : ICommandHandler<AnalyzeCodeContext>
{
    public async Task<CommandResult> ExecuteAsync(
        AnalyzeCodeContext context, CancellationToken cancellationToken)
    {
        if (!sandboxTargets.TryResolve(context.Pipeline, out var sandboxes, out var discoveries))
            return CommandResult.Ok("No Sandboxes/SandboxDiscoveries in pipeline context, skipping");

        var perKey = new Dictionary<string, ProjectMap>(StringComparer.Ordinal);
        var perContext = new Dictionary<string, Dictionary<string, ProjectMap>>(StringComparer.Ordinal);
        context.Pipeline.TryGet<IReadOnlyDictionary<string, string>>(
            ContextKeys.SandboxRepos, out var owners);
        foreach (var (key, sandbox) in sandboxes)
        {
            if (!discoveries.TryGetValue(key, out var discovery)) continue;
            // 2026-09-04-0721: every context in the sandbox, not the group's representative
            // alone. A bootstrap round writes a context.yaml from the map it is handed, and
            // the representative's map describes a different subtree. The analyzer is cached
            // per (sandbox key, workdir), so a re-run of an unchanged context costs nothing.
            foreach (var ctx in SandboxContextList.InOr(context.Pipeline, key, discovery))
            {
                var map = await AnalyzeOneAsync(context, sandbox, key, ctx, cancellationToken);
                if (string.Equals(ctx.ContextName, discovery.ContextName, StringComparison.Ordinal))
                    perKey[key] = map;
                var repoName = owners?.GetValueOrDefault(key) ?? key;
                if (!perContext.TryGetValue(repoName, out var byContext))
                    perContext[repoName] = byContext = new(StringComparer.Ordinal);
                byContext[ctx.ContextName] = map;
            }
        }

        // p0384: per-repo maps are the ONLY output — no collapse to a "primary"
        // (formerly sandboxes.Keys.First(), which made plan/contract/ledger blind
        // to every repo but the first configured one).
        context.Pipeline.Set<IReadOnlyDictionary<string, ProjectMap>>(ContextKeys.RepoProjectMaps, perKey);
        context.Pipeline.Set<IReadOnlyDictionary<string, IReadOnlyDictionary<string, ProjectMap>>>(
            ContextKeys.ContextProjectMaps,
            perContext.ToDictionary(
                kv => kv.Key, kv => (IReadOnlyDictionary<string, ProjectMap>)kv.Value, StringComparer.Ordinal));
        context.Pipeline.Set<IReadOnlyDictionary<string, string>>(
            ContextKeys.RepoCodeMaps,
            perKey.ToDictionary(
                kv => kv.Key,
                kv => ProjectMapTextRenderer.ToCodeMapText(kv.Value),
                StringComparer.Ordinal));

        // p0243: surface what the analyzer understood. The ProjectMap otherwise
        // lived only in the ephemeral sandbox; cache it as markdown (same slot
        // mechanism as result.md/plan.md) so the dashboard can show it after the
        // Analyze step and the operator can judge whether the analysis is right.
        await PersistAnalyzeMarkdownAsync(context.Pipeline, perKey, cancellationToken);

        return CommandResult.Ok($"Analyzed {perKey.Count} context(s)");
    }

    private async Task<ProjectMap> AnalyzeOneAsync(
        AnalyzeCodeContext context, ISandbox sandbox, string key,
        RemoteContextDiscovery discovery, CancellationToken ct)
    {
        var reader = readerFactory.Create(sandbox);
        var subTreePath = SubTreePath(discovery.Workdir);
        var cacheKeyId = CacheKeyForDiscovery(key, discovery);
        // p0240: the repo HEAD SHA invalidates the cache on a source-only commit
        // — without it a stale ProjectMap was served whenever dependency
        // manifests were unchanged, the suspected "AnalyzeCode finished fast,
        // master did nothing" root cause.
        var headSha = await gitOps.GetHeadCommitAsync(sandbox, ct);
        var contentHash = await projectMapCacheKey.ComputeAsync(reader, subTreePath, headSha, ct);
        var map = await mapStore.TryGetAsync(cacheKeyId, contentHash, ct);
        if (map is null)
        {
            logger.LogInformation(
                "{Key}: ProjectMap cache miss — running analyzer at {Path} (HEAD {Sha})",
                key, subTreePath, ShortSha(headSha));
            var agent = context.Pipeline.Resolved().Agent;
            map = await analyzer.AnalyzeAsync(subTreePath, agent, sandbox, ct, repoName: key);
            await mapStore.SetAsync(cacheKeyId, contentHash, map, ct);
            logger.LogInformation(
                "{Key}: analyzed lang={Lang}, modules={Modules}, test_projects={Tests}",
                key, map.PrimaryLanguage, map.Modules.Count, map.TestProjects.Count);
        }
        else
        {
            logger.LogInformation(
                "{Key}: ProjectMap cache hit at HEAD {Sha} ({Tests} test project(s))",
                key, ShortSha(headSha), map.TestProjects.Count);
        }
        return map;
    }

    private static string ShortSha(string sha) =>
        string.IsNullOrEmpty(sha) ? "unknown" : sha[..Math.Min(8, sha.Length)];

    private async Task PersistAnalyzeMarkdownAsync(
        PipelineContext pipeline, IReadOnlyDictionary<string, ProjectMap> maps, CancellationToken ct)
    {
        if (!pipeline.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId))
            return;
        try
        {
            await artifactStore.WriteAnalyzeMarkdownAsync(runId!, AnalyzeMarkdownRenderer.Render(maps), ct);
        }
        catch (Exception ex)
        {
            // Best-effort, like the result/plan cache — a persistence hiccup must
            // not fail the analyze step.
            logger.LogWarning(ex, "Failed to cache analyze.md for run {RunId}", runId);
        }
    }

    private static string SubTreePath(string workdir) =>
        workdir == "." ? Repository.SandboxWorkPath : $"{Repository.SandboxWorkPath}/{workdir}";

    private static string CacheKeyForDiscovery(string key, RemoteContextDiscovery discovery) =>
        discovery.Workdir == "." ? key : $"{key}@{discovery.Workdir}";

}
