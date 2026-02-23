using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services;

/// <summary>
/// Collects raw repository data for LLM interpretation.
/// Two jobs: read config files, collect code samples. No interpretation.
/// </summary>
public sealed class RepoSnapshotCollector(
    ILogger<RepoSnapshotCollector> logger) : IRepoSnapshotCollector
{
    private const int MaxCodeSampleChars = 10_000;
    private const int MaxSampleLines = 80;
    private const int MaxSampleFiles = 15;

    private static readonly HashSet<string> ExcludedDirs = new(StringComparer.OrdinalIgnoreCase)
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

    public RepoSnapshot Collect(string repoPath, DetectedProject project)
    {
        logger.LogInformation("Collecting repo snapshot for {Lang} project at {Path}...",
            project.Language, repoPath);

        var configs = CollectConfigFiles(repoPath);
        var samples = CollectCodeSamples(repoPath, project);

        logger.LogInformation("Snapshot: {Configs} config files, {Samples} code samples",
            configs.Count, samples.Count);

        return new RepoSnapshot(configs, samples);
    }

    internal static IReadOnlyList<string> CollectConfigFiles(string repoPath)
    {
        var contents = new List<string>();

        foreach (var configFile in KnownConfigFiles)
        {
            var fullPath = Path.Combine(repoPath, configFile);
            if (!File.Exists(fullPath)) continue;

            try
            {
                var content = File.ReadAllText(fullPath);
                contents.Add($"### {configFile}\n```\n{content}\n```");
            }
            catch (IOException) { }
        }

        return contents;
    }

    internal static IReadOnlyList<string> CollectCodeSamples(string repoPath, DetectedProject project)
    {
        var extensions = GetSourceExtensions(project.Language);
        var files = FindSourceFiles(repoPath, extensions);
        var samples = new List<string>();
        var totalChars = 0;

        foreach (var file in files.Take(MaxSampleFiles))
        {
            if (totalChars >= MaxCodeSampleChars) break;

            try
            {
                var lines = File.ReadLines(file).Take(MaxSampleLines).ToList();
                var relativePath = Path.GetRelativePath(repoPath, file);
                var content = string.Join('\n', lines);

                if (totalChars + content.Length > MaxCodeSampleChars)
                    content = content[..(MaxCodeSampleChars - totalChars)];

                samples.Add($"### {relativePath}\n{content}");
                totalChars += content.Length;
            }
            catch (IOException) { }
        }

        return samples;
    }

    private static string[] GetSourceExtensions(string language) => language switch
    {
        "C#" => ["*.cs"],
        "TypeScript" => ["*.ts", "*.tsx"],
        "JavaScript" => ["*.js", "*.jsx"],
        "Python" => ["*.py"],
        _ => ["*.cs", "*.ts", "*.py", "*.js"]
    };

    private static IEnumerable<string> FindSourceFiles(string repoPath, string[] extensions)
    {
        return extensions
            .SelectMany(ext =>
            {
                try
                {
                    return Directory.GetFiles(repoPath, ext, SearchOption.AllDirectories);
                }
                catch { return []; }
            })
            .Where(f => !IsExcludedPath(f, repoPath))
            .OrderByDescending(f => new FileInfo(f).Length);
    }

    private static bool IsExcludedPath(string fullPath, string repoPath)
    {
        var relative = Path.GetRelativePath(repoPath, fullPath);
        return ExcludedDirs.Any(d => relative.Contains(d, StringComparison.OrdinalIgnoreCase));
    }
}
