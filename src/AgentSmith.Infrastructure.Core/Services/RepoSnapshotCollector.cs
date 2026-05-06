using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// Collects raw repository data for LLM interpretation via ISandboxFileReader.
/// Three jobs: read config files, collect code samples, generate directory tree.
/// No interpretation — just data collection.
/// </summary>
public sealed class RepoSnapshotCollector(
    ILogger<RepoSnapshotCollector> logger) : IRepoSnapshotCollector
{
    private const int MaxCodeSampleChars = 10_000;
    private const int MaxSampleLines = 80;
    private const int MaxSampleFiles = 15;
    private const int MaxTreeDepth = 4;
    private const int MaxTreeLines = 200;

    internal static readonly HashSet<string> ExcludedDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "node_modules", "bin", "obj", ".vs", ".idea",
        "__pycache__", ".mypy_cache", ".pytest_cache", "dist", "build",
        ".next", ".nuxt", "coverage", ".terraform", "vendor"
    };

    private static readonly string[] KnownConfigFiles =
    [
        ".editorconfig", ".eslintrc.json", ".eslintrc.js", ".eslintrc.yml", ".eslintrc",
        ".prettierrc", ".prettierrc.json", ".prettierrc.yml",
        "ruff.toml", ".csharpierrc", ".csharpierrc.json"
    ];

    public async Task<RepoSnapshot> CollectAsync(
        ISandboxFileReader reader, string repoPath, DetectedProject project, CancellationToken cancellationToken)
    {
        logger.LogInformation("Collecting repo snapshot for {Lang} project at {Path}...",
            project.Language, repoPath);

        var configs = await CollectConfigFilesAsync(reader, repoPath, cancellationToken);
        var samples = await CollectCodeSamplesAsync(reader, repoPath, project, cancellationToken);
        var tree = await GenerateTreeAsync(reader, repoPath, MaxTreeDepth, cancellationToken);

        logger.LogInformation("Snapshot: {Configs} config files, {Samples} code samples, tree {TreeChars} chars",
            configs.Count, samples.Count, tree.Length);

        return new RepoSnapshot(configs, samples, tree);
    }

    internal static async Task<IReadOnlyList<string>> CollectConfigFilesAsync(
        ISandboxFileReader reader, string repoPath, CancellationToken cancellationToken)
    {
        var contents = new List<string>();

        foreach (var configFile in KnownConfigFiles)
        {
            var content = await reader.TryReadAsync(Path.Combine(repoPath, configFile), cancellationToken);
            if (content is not null)
                contents.Add($"### {configFile}\n```\n{content}\n```");
        }

        return contents;
    }

    internal static async Task<IReadOnlyList<string>> CollectCodeSamplesAsync(
        ISandboxFileReader reader, string repoPath, DetectedProject project, CancellationToken cancellationToken)
    {
        var extensions = GetSourceExtensions(project.Language);
        var files = await FindSourceFilesAsync(reader, repoPath, extensions, cancellationToken);
        var samples = new List<string>();
        var totalChars = 0;

        foreach (var file in files.Take(MaxSampleFiles))
        {
            if (totalChars >= MaxCodeSampleChars) break;

            var content = await reader.TryReadAsync(file, cancellationToken);
            if (content is null) continue;

            var lines = content.Split('\n').Take(MaxSampleLines);
            var snippet = string.Join('\n', lines);
            var relativePath = RelativeOf(file, repoPath);

            if (totalChars + snippet.Length > MaxCodeSampleChars)
                snippet = snippet[..(MaxCodeSampleChars - totalChars)];

            samples.Add($"### {relativePath}\n{snippet}");
            totalChars += snippet.Length;
        }

        return samples;
    }

    internal static async Task<string> GenerateTreeAsync(
        ISandboxFileReader reader, string rootPath, int maxDepth, CancellationToken cancellationToken)
    {
        var entries = await reader.ListAsync(rootPath, maxDepth, cancellationToken);
        var lines = TreeRenderer.Render(rootPath, entries, maxDepth, ExcludedDirs);
        return string.Join('\n', lines.Take(MaxTreeLines));
    }

    private static string[] GetSourceExtensions(string language) => language switch
    {
        "C#" => [".cs"],
        "TypeScript" => [".ts", ".tsx"],
        "JavaScript" => [".js", ".jsx"],
        "Python" => [".py"],
        _ => [".cs", ".ts", ".py", ".js"]
    };

    private static async Task<IReadOnlyList<string>> FindSourceFilesAsync(
        ISandboxFileReader reader, string repoPath, string[] extensions, CancellationToken cancellationToken)
    {
        var entries = await reader.ListAsync(repoPath, maxDepth: 8, cancellationToken);
        return entries
            .Where(e => extensions.Any(ext => e.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            .Where(e => !IsExcludedPath(e, repoPath))
            .OrderBy(e => e, StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsExcludedPath(string fullPath, string repoPath)
    {
        var relative = RelativeOf(fullPath, repoPath);
        var segments = relative.Split('/');
        return segments.Any(s => ExcludedDirs.Contains(s));
    }

    private static string RelativeOf(string fullPath, string repoPath)
    {
        var rel = fullPath.Length > repoPath.Length ? fullPath[repoPath.Length..] : fullPath;
        return rel.TrimStart('/');
    }
}
