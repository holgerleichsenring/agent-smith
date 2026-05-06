using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// Orchestrates language detection by iterating registered ILanguageDetector
/// implementations. First non-null result wins. Adds infrastructure and readme
/// detection (language-agnostic). Uses zero LLM tokens.
/// </summary>
public sealed class ProjectDetector(
    IEnumerable<ILanguageDetector> detectors,
    ILogger<ProjectDetector> logger) : IProjectDetector
{
    private const int MaxReadmeWords = 300;

    public async Task<DetectedProject> DetectAsync(
        ISandboxFileReader reader, string repoPath, CancellationToken cancellationToken)
    {
        logger.LogInformation("Detecting project type in {Path}...", repoPath);

        var lang = await DetectLanguageAsync(reader, repoPath, cancellationToken);
        var infrastructure = await DetectInfrastructureAsync(reader, repoPath, cancellationToken);
        var readme = await ReadReadmeExcerptAsync(reader, repoPath, cancellationToken);

        var result = new DetectedProject(
            lang.Language, lang.Runtime, lang.PackageManager,
            lang.BuildCommand, lang.TestCommand, lang.Frameworks,
            infrastructure, lang.KeyFiles, lang.Sdks, readme);

        logger.LogInformation(
            "Detected: {Lang} ({Runtime}), build={Build}, test={Test}, {KeyFileCount} key files",
            result.Language, result.Runtime ?? "unknown", result.BuildCommand ?? "none",
            result.TestCommand ?? "none", result.KeyFiles.Count);

        return result;
    }

    private async Task<LanguageDetectionResult> DetectLanguageAsync(
        ISandboxFileReader reader, string repoPath, CancellationToken cancellationToken)
    {
        foreach (var detector in detectors)
        {
            var result = await detector.DetectAsync(reader, repoPath, cancellationToken);
            if (result is not null)
                return result;
        }

        return new LanguageDetectionResult(
            "Unknown", null, null, null, null, [], [], []);
    }

    private static async Task<IReadOnlyList<string>> DetectInfrastructureAsync(
        ISandboxFileReader reader, string repoPath, CancellationToken cancellationToken)
    {
        var entries = await reader.ListAsync(repoPath, maxDepth: 6, cancellationToken);
        var infra = new List<string>();

        if (await reader.ExistsAsync(Path.Combine(repoPath, "Dockerfile"), cancellationToken)
            || entries.Any(e => LastSegment(e).Equals("Dockerfile", StringComparison.Ordinal)))
            infra.Add("Docker");
        if (entries.Any(e => LastSegment(e).StartsWith("docker-compose", StringComparison.OrdinalIgnoreCase)
            && (e.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)
                || e.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))))
            infra.Add("Docker-Compose");
        if (await reader.ExistsAsync(Path.Combine(repoPath, "kustomization.yaml"), cancellationToken)
            || entries.Any(e => IsDirectoryEntry(entries, Path.Combine(repoPath, "k8s"), e))
            || entries.Any(e => IsDirectoryEntry(entries, Path.Combine(repoPath, "deploy", "k8s"), e)))
            infra.Add("K8s");

        var topEntries = await reader.ListAsync(repoPath, maxDepth: 1, cancellationToken);
        if (topEntries.Any(e => e.Equals(Path.Combine(repoPath, "terraform"), StringComparison.Ordinal))
            || topEntries.Any(e => e.EndsWith(".tf", StringComparison.OrdinalIgnoreCase)))
            infra.Add("Terraform");

        if (await DirectoryExistsAsync(reader, Path.Combine(repoPath, ".github", "workflows"), cancellationToken))
            infra.Add("GitHub-Actions");
        if (await reader.ExistsAsync(Path.Combine(repoPath, "azure-pipelines.yml"), cancellationToken))
            infra.Add("Azure-DevOps-Pipelines");
        if (await reader.ExistsAsync(Path.Combine(repoPath, ".gitlab-ci.yml"), cancellationToken))
            infra.Add("GitLab-CI");
        if (await reader.ExistsAsync(Path.Combine(repoPath, "Jenkinsfile"), cancellationToken))
            infra.Add("Jenkins");

        return infra;
    }

    private async Task<string?> ReadReadmeExcerptAsync(
        ISandboxFileReader reader, string repoPath, CancellationToken cancellationToken)
    {
        try
        {
            var content = await reader.TryReadAsync(Path.Combine(repoPath, "README.md"), cancellationToken);
            if (content is null) return null;

            var words = content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(' ', words.Take(MaxReadmeWords));
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Could not read README.md");
            return null;
        }
    }

    private static async Task<bool> DirectoryExistsAsync(
        ISandboxFileReader reader, string path, CancellationToken cancellationToken)
    {
        var entries = await reader.ListAsync(path, maxDepth: 1, cancellationToken);
        return entries.Count > 0;
    }

    private static bool IsDirectoryEntry(IReadOnlyList<string> all, string candidate, string entry) =>
        entry.Equals(candidate, StringComparison.Ordinal);

    private static string LastSegment(string path)
    {
        var idx = path.LastIndexOf('/');
        return idx < 0 ? path : path[(idx + 1)..];
    }
}
