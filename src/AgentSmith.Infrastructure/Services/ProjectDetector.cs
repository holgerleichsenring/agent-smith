using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services;

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

    public DetectedProject Detect(string repoPath)
    {
        logger.LogInformation("Detecting project type in {Path}...", repoPath);

        var lang = DetectLanguage(repoPath);
        var infrastructure = DetectInfrastructure(repoPath);
        var readme = ReadReadmeExcerpt(repoPath);

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

    private LanguageDetectionResult DetectLanguage(string repoPath)
    {
        foreach (var detector in detectors)
        {
            var result = detector.Detect(repoPath);
            if (result is not null)
                return result;
        }

        return new LanguageDetectionResult(
            "Unknown", null, null, null, null, [], [], []);
    }

    private static IReadOnlyList<string> DetectInfrastructure(string repoPath)
    {
        var infra = new List<string>();

        if (File.Exists(Path.Combine(repoPath, "Dockerfile")))
            infra.Add("Docker");
        if (Directory.GetFiles(repoPath, "docker-compose*.yml", SearchOption.TopDirectoryOnly).Length > 0
            || Directory.GetFiles(repoPath, "docker-compose*.yaml", SearchOption.TopDirectoryOnly).Length > 0)
            infra.Add("Docker-Compose");
        if (Directory.Exists(Path.Combine(repoPath, "k8s"))
            || File.Exists(Path.Combine(repoPath, "kustomization.yaml")))
            infra.Add("K8s");
        if (Directory.Exists(Path.Combine(repoPath, "terraform"))
            || Directory.GetFiles(repoPath, "*.tf", SearchOption.TopDirectoryOnly).Length > 0)
            infra.Add("Terraform");

        if (Directory.Exists(Path.Combine(repoPath, ".github", "workflows")))
            infra.Add("GitHub-Actions");
        if (File.Exists(Path.Combine(repoPath, "azure-pipelines.yml")))
            infra.Add("Azure-DevOps-Pipelines");
        if (File.Exists(Path.Combine(repoPath, ".gitlab-ci.yml")))
            infra.Add("GitLab-CI");
        if (File.Exists(Path.Combine(repoPath, "Jenkinsfile")))
            infra.Add("Jenkins");

        return infra;
    }

    private string? ReadReadmeExcerpt(string repoPath)
    {
        var readmePath = Path.Combine(repoPath, "README.md");
        if (!File.Exists(readmePath)) return null;

        try
        {
            var content = File.ReadAllText(readmePath);
            var words = content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(' ', words.Take(MaxReadmeWords));
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Could not read {File}", readmePath);
            return null;
        }
    }
}
