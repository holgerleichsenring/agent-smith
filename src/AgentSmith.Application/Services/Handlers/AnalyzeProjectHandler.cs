using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Replaces AnalyzeCodeHandler: runs the agentic ProjectAnalyzer when no
/// cached project-map.json exists for the current dependency-manifest hash,
/// otherwise loads the cached map. Persists the result to .agentsmith/project-map.json
/// in the analyzed repo. Transitional dual-population: writes ContextKeys.ProjectMap
/// (primary) AND ContextKeys.CodeAnalysis + ContextKeys.CodeMap (legacy view) so
/// unmigrated downstream consumers keep working until p0110c removes them.
/// </summary>
public sealed class AnalyzeProjectHandler(
    IProjectAnalyzer analyzer,
    IProjectMetaResolver metaResolver,
    ILogger<AnalyzeProjectHandler> logger) : ICommandHandler<AnalyzeCodeContext>
{
    private const string MapFileName = "project-map.json";
    private const string CacheKeyFileName = "project-map.cache-key";

    public async Task<CommandResult> ExecuteAsync(
        AnalyzeCodeContext context, CancellationToken cancellationToken)
    {
        var repoPath = context.Repository.LocalPath;
        var metaDir = metaResolver.Resolve(repoPath) ?? Path.Combine(repoPath, ".agentsmith");
        Directory.CreateDirectory(metaDir);

        var cacheKey = ProjectMapCacheKey.Compute(repoPath);
        var map = TryLoadCached(metaDir, cacheKey);
        if (map is null)
        {
            logger.LogInformation("ProjectMap cache miss — running analyzer for {Path}", repoPath);
            var agent = context.Pipeline.Resolved().Agent;
            map = await analyzer.AnalyzeAsync(repoPath, agent, cancellationToken);
            PersistCache(metaDir, map, cacheKey);
            logger.LogInformation(
                "ProjectMap analyzed: {Lang}, {Modules} module(s), {Tests} test project(s)",
                map.PrimaryLanguage, map.Modules.Count, map.TestProjects.Count);
        }
        else
        {
            logger.LogInformation(
                "ProjectMap cache hit ({Tests} test project(s)) — skipping analyzer", map.TestProjects.Count);
        }

        context.Pipeline.Set(ContextKeys.ProjectMap, map);

        // Transitional: feed legacy consumers from the same source until p0110c removes them.
        context.Pipeline.Set(ContextKeys.CodeAnalysis, ToCodeAnalysis(map, repoPath));
        context.Pipeline.Set(ContextKeys.CodeMap, ToCodeMapText(map));

        return CommandResult.Ok(
            $"Analyzed: {map.Modules.Count} module(s), {map.TestProjects.Count} test project(s)");
    }

    private static ProjectMap? TryLoadCached(string metaDir, string cacheKey)
    {
        if (string.IsNullOrEmpty(cacheKey)) return null;

        var keyPath = Path.Combine(metaDir, CacheKeyFileName);
        var mapPath = Path.Combine(metaDir, MapFileName);
        if (!File.Exists(keyPath) || !File.Exists(mapPath)) return null;
        if (File.ReadAllText(keyPath).Trim() != cacheKey) return null;

        try
        {
            var json = File.ReadAllText(mapPath);
            return JsonSerializer.Deserialize<ProjectMap>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static void PersistCache(string metaDir, ProjectMap map, string cacheKey)
    {
        var mapPath = Path.Combine(metaDir, MapFileName);
        var keyPath = Path.Combine(metaDir, CacheKeyFileName);
        File.WriteAllText(mapPath, JsonSerializer.Serialize(map, JsonOptions));
        if (!string.IsNullOrEmpty(cacheKey))
            File.WriteAllText(keyPath, cacheKey);
    }

    private static CodeAnalysis ToCodeAnalysis(ProjectMap map, string repoPath)
    {
        var fileStructure = SafeListFiles(repoPath);
        var dependencies = map.Frameworks.ToList();
        var framework = map.Frameworks.FirstOrDefault();
        return new CodeAnalysis(fileStructure, dependencies, framework, map.PrimaryLanguage);
    }

    private static IReadOnlyList<string> SafeListFiles(string repoPath)
    {
        try
        {
            return Directory.GetFiles(repoPath, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(repoPath, f))
                .Where(f => !f.Contains("/.git/") && !f.StartsWith(".git" + Path.DirectorySeparatorChar))
                .OrderBy(f => f)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static string ToCodeMapText(ProjectMap map) =>
        // Render a minimal YAML-ish view so prompts that consume CodeMap as a string
        // get something useful. Full schema lives in project-map.json.
        $"primary_language: {map.PrimaryLanguage}\n" +
        $"frameworks: [{string.Join(", ", map.Frameworks)}]\n" +
        $"modules:\n" +
        string.Join('\n', map.Modules.Select(m => $"  - path: {m.Path}\n    role: {m.Role}")) +
        (map.TestProjects.Count == 0 ? "" :
            "\ntest_projects:\n" +
            string.Join('\n', map.TestProjects.Select(t =>
                $"  - path: {t.Path}\n    framework: {t.Framework}\n    file_count: {t.FileCount}")));

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };
}
