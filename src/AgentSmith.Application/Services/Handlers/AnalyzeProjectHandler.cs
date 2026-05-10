using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Runs the agentic ProjectAnalyzer when no cached project-map.json exists
/// for the current dependency-manifest hash, otherwise loads the cached map.
/// The cache is host-side, keyed by the repo's remote URL via
/// <see cref="IAgentSmithPaths"/>, so it never lands inside the project's
/// <c>.agentsmith/</c> directory where blanket <c>git add -A</c> would commit
/// it. ContextKeys.CodeMap holds a YAML-ish text rendering for prompt-builders
/// that consume the map as a single string (separate from ContextKeys.ProjectMap
/// which is the structured representation).
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
        var sandbox = context.Pipeline.Get<ISandbox>(ContextKeys.Sandbox);
        var reader = readerFactory.Create(sandbox);
        var repoPath = context.Repository.LocalPath;
        var cacheDir = paths.ProjectCacheDir(context.Repository.RemoteUrl);

        // Cache-key is hashed over manifests inside the repo (sandbox-side);
        // cache JSON itself lives on the host so init-PRs stay clean.
        var cacheKey = await ProjectMapCacheKey.ComputeAsync(reader, repoPath, cancellationToken);
        var map = await TryLoadCachedAsync(cacheDir, cacheKey, cancellationToken);
        if (map is null)
        {
            logger.LogInformation("ProjectMap cache miss — running analyzer for {Path}", repoPath);
            var agent = context.Pipeline.Resolved().Agent;
            map = await analyzer.AnalyzeAsync(repoPath, agent, sandbox, cancellationToken);
            await PersistCacheAsync(cacheDir, map, cacheKey, cancellationToken);
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
        context.Pipeline.Set(ContextKeys.CodeMap, ToCodeMapText(map));

        return CommandResult.Ok(
            $"Analyzed: {map.Modules.Count} module(s), {map.TestProjects.Count} test project(s)");
    }

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
