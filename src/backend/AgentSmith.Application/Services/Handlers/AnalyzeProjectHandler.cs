using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
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
/// (now keyed by sandbox key) and legacy single-slot ContextKeys.ProjectMap
/// / ContextKeys.CodeMap from the first sandbox key. The host-side cache is
/// keyed by (repo-URL + workdir-suffix) so monorepo sub-trees do not
/// stomp each other's project-map.json.
/// </summary>
public sealed class AnalyzeProjectHandler(
    IProjectAnalyzer analyzer,
    ISandboxFileReaderFactory readerFactory,
    IAgentSmithPaths paths,
    ILogger<AnalyzeProjectHandler> logger) : ICommandHandler<AnalyzeCodeContext>
{
    private const string MapFileName = "project-map.json";
    private const string CacheKeyFileName = "project-map.cache-key";

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
            context.Pipeline.Set(ContextKeys.CodeMap, ToCodeMapText(primary));
        }
        return CommandResult.Ok($"Analyzed {perKey.Count} context(s)");
    }

    private async Task<ProjectMap> AnalyzeOneAsync(
        AnalyzeCodeContext context, ISandbox sandbox, string key,
        RemoteContextDiscovery discovery, CancellationToken ct)
    {
        var reader = readerFactory.Create(sandbox);
        var subTreePath = SubTreePath(discovery.Workdir);
        var cacheDir = paths.ProjectCacheDir(CacheKeyForDiscovery(key, discovery));
        var cacheKey = await ProjectMapCacheKey.ComputeAsync(reader, subTreePath, ct);
        var map = await TryLoadCachedAsync(cacheDir, cacheKey, ct);
        if (map is null)
        {
            logger.LogInformation("{Key}: ProjectMap cache miss — running analyzer at {Path}", key, subTreePath);
            var agent = context.Pipeline.Resolved().Agent;
            map = await analyzer.AnalyzeAsync(subTreePath, agent, sandbox, ct, repoName: key);
            await PersistCacheAsync(cacheDir, map, cacheKey, ct);
            logger.LogInformation(
                "{Key}: analyzed lang={Lang}, modules={Modules}, test_projects={Tests}",
                key, map.PrimaryLanguage, map.Modules.Count, map.TestProjects.Count);
        }
        else
        {
            logger.LogInformation(
                "{Key}: ProjectMap cache hit ({Tests} test project(s))", key, map.TestProjects.Count);
        }
        return map;
    }

    private static string SubTreePath(string workdir) =>
        workdir == "." ? Repository.SandboxWorkPath : $"{Repository.SandboxWorkPath}/{workdir}";

    private static string CacheKeyForDiscovery(string key, RemoteContextDiscovery discovery) =>
        discovery.Workdir == "." ? key : $"{key}@{discovery.Workdir}";

    private static string ToCodeMapText(ProjectMap map) =>
        $"primary_language: {map.PrimaryLanguage}\n" +
        $"frameworks: [{string.Join(", ", map.Frameworks)}]\n" +
        $"modules:\n" +
        string.Join('\n', map.Modules.Select(m => $"  - path: {m.Path}\n    role: {m.Role}")) +
        (map.TestProjects.Count == 0 ? "" :
            "\ntest_projects:\n" +
            string.Join('\n', map.TestProjects.Select(t =>
                $"  - path: {t.Path}\n    framework: {t.Framework}\n    file_count: {t.FileCount}")));

    private static async Task<ProjectMap?> TryLoadCachedAsync(
        string cacheDir, string cacheKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(cacheKey)) return null;

        var keyPath = Path.Combine(cacheDir, CacheKeyFileName);
        if (!File.Exists(keyPath)) return null;
        var keyContent = await File.ReadAllTextAsync(keyPath, cancellationToken);
        if (keyContent.Trim() != cacheKey) return null;

        var mapPath = Path.Combine(cacheDir, MapFileName);
        if (!File.Exists(mapPath)) return null;
        var json = await File.ReadAllTextAsync(mapPath, cancellationToken);

        try
        {
            return JsonSerializer.Deserialize<ProjectMap>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static async Task PersistCacheAsync(
        string cacheDir, ProjectMap map, string cacheKey, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(cacheDir);
        await File.WriteAllTextAsync(
            Path.Combine(cacheDir, MapFileName),
            JsonSerializer.Serialize(map, JsonOptions),
            cancellationToken);
        if (!string.IsNullOrEmpty(cacheKey))
            await File.WriteAllTextAsync(
                Path.Combine(cacheDir, CacheKeyFileName), cacheKey, cancellationToken);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };
}
