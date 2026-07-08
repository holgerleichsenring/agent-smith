using System.Text;
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
/// sub-folder for monorepo contexts). Populates ContextKeys.RepoProjectMaps
/// (keyed by sandbox key) and legacy single-slot ContextKeys.ProjectMap
/// / ContextKeys.CodeMap from the first sandbox key. Cache I/O goes through
/// p0182's IProjectMapStore — Redis on the server, disk on the CLI.
/// </summary>
public sealed class AnalyzeProjectHandler(
    IProjectAnalyzer analyzer,
    ISandboxFileReaderFactory readerFactory,
    IProjectMapStore mapStore,
    SandboxGitOperations gitOps,
    IRunArtifactStore artifactStore,
    ILogger<AnalyzeProjectHandler> logger) : ICommandHandler<AnalyzeCodeContext>
{
    public async Task<CommandResult> ExecuteAsync(
        AnalyzeCodeContext context, CancellationToken cancellationToken)
    {
        if (!SandboxTargets.TryResolve(context.Pipeline, out var sandboxes, out var discoveries))
            return CommandResult.Ok("No Sandboxes/SandboxDiscoveries in pipeline context, skipping");

        var perKey = new Dictionary<string, ProjectMap>(StringComparer.Ordinal);
        foreach (var (key, sandbox) in sandboxes)
        {
            if (!discoveries.TryGetValue(key, out var discovery)) continue;
            var map = await AnalyzeOneAsync(context, sandbox, key, discovery, cancellationToken);
            perKey[key] = map;
        }

        context.Pipeline.Set<IReadOnlyDictionary<string, ProjectMap>>(ContextKeys.RepoProjectMaps, perKey);
        var primaryKey = sandboxes.Keys.First();
        if (perKey.TryGetValue(primaryKey, out var primary))
        {
            context.Pipeline.Set(ContextKeys.ProjectMap, primary);
            context.Pipeline.Set(ContextKeys.CodeMap, ProjectMapTextRenderer.ToCodeMapText(primary));
        }

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
        var contentHash = await ProjectMapCacheKey.ComputeAsync(reader, subTreePath, headSha, ct);
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
            await artifactStore.WriteAnalyzeMarkdownAsync(runId!, RenderAnalyzeMarkdown(maps), ct);
        }
        catch (Exception ex)
        {
            // Best-effort, like the result/plan cache — a persistence hiccup must
            // not fail the analyze step.
            logger.LogWarning(ex, "Failed to cache analyze.md for run {RunId}", runId);
        }
    }

    // p0243: render the per-repo ProjectMap(s) as operator-readable markdown —
    // language, build/test commands, modules, test projects, conventions. This is
    // "what the agent understood before it started"; the dashboard shows it after
    // the Analyze step so the operator isn't flying blind on the agent's intent.
    private static string RenderAnalyzeMarkdown(IReadOnlyDictionary<string, ProjectMap> maps)
    {
        var sb = new StringBuilder();
        sb.Append("# Analyze — what the agent understood\n\n");
        sb.Append($"{maps.Count} context(s) analyzed.\n");
        foreach (var (key, m) in maps)
        {
            sb.Append($"\n## {key}\n\n");
            sb.Append($"- **Language:** {m.PrimaryLanguage}\n");
            if (m.Frameworks.Count > 0)
                sb.Append($"- **Frameworks:** {string.Join(", ", m.Frameworks)}\n");
            sb.Append($"- **Build:** {Code(m.Ci.BuildCommand)}\n");
            sb.Append($"- **Test:** {Code(m.Ci.TestCommand)}\n");
            sb.Append($"- **Prerequisites:** {Code(m.Prerequisites)}\n");
            if (m.EntryPoints.Count > 0)
                sb.Append($"- **Entry points:** {string.Join(", ", m.EntryPoints.Select(e => $"`{e}`"))}\n");

            sb.Append($"\n**Modules ({m.Modules.Count})**\n\n");
            foreach (var mod in m.Modules)
                sb.Append($"- `{mod.Path}` — {mod.Role}\n");

            sb.Append($"\n**Test projects ({m.TestProjects.Count})**\n\n");
            if (m.TestProjects.Count == 0)
                sb.Append("- _none discovered_\n");
            foreach (var t in m.TestProjects)
                sb.Append($"- `{t.Path}` — {t.Framework} ({t.FileCount} file(s))\n");

            if (m.Conventions is { } c &&
                (c.NamingPattern is not null || c.TestLayout is not null || c.ErrorHandling is not null))
            {
                sb.Append("\n**Conventions**\n\n");
                if (c.NamingPattern is not null) sb.Append($"- naming: {c.NamingPattern}\n");
                if (c.TestLayout is not null) sb.Append($"- test layout: {c.TestLayout}\n");
                if (c.ErrorHandling is not null) sb.Append($"- error handling: {c.ErrorHandling}\n");
            }
        }
        return sb.ToString();
    }

    private static string Code(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "_n/a_" : $"`{value}`";

    private static string SubTreePath(string workdir) =>
        workdir == "." ? Repository.SandboxWorkPath : $"{Repository.SandboxWorkPath}/{workdir}";

    private static string CacheKeyForDiscovery(string key, RemoteContextDiscovery discovery) =>
        discovery.Workdir == "." ? key : $"{key}@{discovery.Workdir}";

}
