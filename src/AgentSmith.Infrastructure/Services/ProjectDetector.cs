using System.Text.Json;
using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services;

/// <summary>
/// Deterministic project detector that scans marker files to identify
/// language, runtime, package manager, build/test commands, and key dependencies.
/// Uses zero LLM tokens — pure filesystem analysis.
/// </summary>
public sealed partial class ProjectDetector(ILogger<ProjectDetector> logger) : IProjectDetector
{
    private const int MaxReadmeWords = 300;

    public DetectedProject Detect(string repoPath)
    {
        logger.LogInformation("Detecting project type in {Path}...", repoPath);

        var (language, runtime, packageManager, buildCmd, testCmd, frameworks, keyFiles, sdks) =
            DetectDotNet(repoPath) ??
            DetectTypeScript(repoPath) ??
            DetectPython(repoPath) ??
            FallbackDetection();

        var infrastructure = DetectInfrastructure(repoPath);
        var readme = ReadReadmeExcerpt(repoPath);

        var result = new DetectedProject(
            language, runtime, packageManager, buildCmd, testCmd,
            frameworks, infrastructure, keyFiles, sdks, readme);

        logger.LogInformation(
            "Detected: {Lang} ({Runtime}), build={Build}, test={Test}, {KeyFileCount} key files",
            result.Language, result.Runtime ?? "unknown", result.BuildCommand ?? "none",
            result.TestCommand ?? "none", result.KeyFiles.Count);

        return result;
    }

    private DetectionResult? DetectDotNet(string repoPath)
    {
        var slnFiles = Directory.GetFiles(repoPath, "*.sln", SearchOption.TopDirectoryOnly);
        var csprojFiles = Directory.GetFiles(repoPath, "*.csproj", SearchOption.AllDirectories);

        if (slnFiles.Length == 0 && csprojFiles.Length == 0)
            return null;

        var keyFiles = new List<string>();
        keyFiles.AddRange(csprojFiles.Select(f => Path.GetRelativePath(repoPath, f)));

        var globalJson = Path.Combine(repoPath, "global.json");
        if (File.Exists(globalJson))
            keyFiles.Add("global.json");

        string? runtime = null;
        var sdks = new List<string>();
        var frameworks = new List<string>();
        var hasTestSdk = false;

        foreach (var csproj in csprojFiles)
        {
            var content = TryReadFile(csproj);
            if (content is null) continue;

            runtime ??= ExtractTargetFramework(content);

            foreach (Match m in PackageReferenceRegex().Matches(content))
            {
                var pkg = m.Groups[1].Value;
                sdks.Add(pkg);

                if (pkg.Equals("Microsoft.NET.Test.Sdk", StringComparison.OrdinalIgnoreCase))
                    hasTestSdk = true;
            }
        }

        var runtimeDisplay = runtime is not null ? FormatDotNetRuntime(runtime) : ".NET";
        var testingFrameworks = sdks.Where(IsDotNetTestFramework).Distinct().ToList();
        frameworks.AddRange(testingFrameworks);

        return new DetectionResult(
            Language: "C#",
            Runtime: runtimeDisplay,
            PackageManager: "NuGet",
            BuildCommand: "dotnet build",
            TestCommand: hasTestSdk ? "dotnet test" : null,
            Frameworks: frameworks.Distinct().ToList(),
            KeyFiles: keyFiles,
            Sdks: sdks.Distinct().ToList());
    }

    private DetectionResult? DetectTypeScript(string repoPath)
    {
        var packageJsonPath = Path.Combine(repoPath, "package.json");
        var tsconfigPath = Path.Combine(repoPath, "tsconfig.json");
        var denoJsonPath = Path.Combine(repoPath, "deno.json");

        if (!File.Exists(packageJsonPath) && !File.Exists(denoJsonPath))
            return null;

        var keyFiles = new List<string>();
        var sdks = new List<string>();
        var frameworks = new List<string>();
        string? buildCmd = null;
        string? testCmd = null;
        var language = File.Exists(tsconfigPath) ? "TypeScript" : "JavaScript";

        if (File.Exists(packageJsonPath))
        {
            keyFiles.Add("package.json");
            if (File.Exists(tsconfigPath)) keyFiles.Add("tsconfig.json");

            var content = TryReadFile(packageJsonPath);
            if (content is not null)
            {
                try
                {
                    using var doc = JsonDocument.Parse(content);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("scripts", out var scripts))
                    {
                        if (scripts.TryGetProperty("build", out _))
                            buildCmd = "npm run build";
                        if (scripts.TryGetProperty("test", out _))
                            testCmd = "npm test";
                    }

                    sdks.AddRange(ExtractJsonDependencies(root, "dependencies"));
                    sdks.AddRange(ExtractJsonDependencies(root, "devDependencies"));
                }
                catch (JsonException ex)
                {
                    logger.LogWarning(ex, "Failed to parse package.json");
                }
            }
        }

        // Framework detection from config files
        DetectJsFramework(repoPath, "next.config.*", "Next.js", frameworks);
        DetectJsFramework(repoPath, "angular.json", "Angular", frameworks);
        DetectJsFramework(repoPath, "vite.config.*", "Vite", frameworks);
        DetectJsFramework(repoPath, "nuxt.config.*", "Nuxt", frameworks);
        DetectJsFramework(repoPath, "svelte.config.*", "SvelteKit", frameworks);
        DetectJsFramework(repoPath, "remix.config.*", "Remix", frameworks);
        DetectJsFramework(repoPath, "astro.config.*", "Astro", frameworks);

        // Test framework detection (more specific than scripts.test)
        testCmd ??= DetectJsTestFramework(repoPath);

        // Package manager detection
        var packageManager = DetectJsPackageManager(repoPath);

        // Adjust build/test commands for non-npm package managers
        if (packageManager is "pnpm" or "yarn" or "bun")
        {
            buildCmd = buildCmd?.Replace("npm run build", $"{packageManager} run build");
            testCmd = testCmd?.Replace("npm test", $"{packageManager} test");
        }

        var runtime = File.Exists(denoJsonPath) ? "Deno" : "Node.js";

        return new DetectionResult(
            Language: language,
            Runtime: runtime,
            PackageManager: packageManager,
            BuildCommand: buildCmd,
            TestCommand: testCmd,
            Frameworks: frameworks,
            KeyFiles: keyFiles,
            Sdks: sdks.Distinct().ToList());
    }

    private DetectionResult? DetectPython(string repoPath)
    {
        var pyprojectPath = Path.Combine(repoPath, "pyproject.toml");
        var setupPyPath = Path.Combine(repoPath, "setup.py");
        var requirementsPath = Path.Combine(repoPath, "requirements.txt");
        var pipfilePath = Path.Combine(repoPath, "Pipfile");

        if (!File.Exists(pyprojectPath) && !File.Exists(setupPyPath)
            && !File.Exists(requirementsPath) && !File.Exists(pipfilePath))
            return null;

        var keyFiles = new List<string>();
        var sdks = new List<string>();
        string? testCmd = null;
        string? packageManager = null;

        if (File.Exists(pyprojectPath))
        {
            keyFiles.Add("pyproject.toml");
            var content = TryReadFile(pyprojectPath) ?? "";

            if (content.Contains("[tool.pytest]") || content.Contains("[tool.pytest.ini_options]"))
                testCmd = "pytest";
            if (content.Contains("[tool.poetry]"))
                packageManager = "poetry";
            else if (content.Contains("hatchling") || content.Contains("[tool.hatch]"))
                packageManager = "hatch";
        }

        if (File.Exists(setupPyPath)) keyFiles.Add("setup.py");
        if (File.Exists(requirementsPath)) keyFiles.Add("requirements.txt");
        if (File.Exists(pipfilePath)) keyFiles.Add("Pipfile");

        // Package manager detection (order matters — more specific first)
        packageManager ??= DetectPythonPackageManager(repoPath);

        // Test command detection
        testCmd ??= DetectPythonTestCommand(repoPath);

        return new DetectionResult(
            Language: "Python",
            Runtime: "Python",
            PackageManager: packageManager,
            BuildCommand: null,
            TestCommand: testCmd,
            Frameworks: [],
            KeyFiles: keyFiles,
            Sdks: sdks);
    }

    private static DetectionResult FallbackDetection() =>
        new("Unknown", null, null, null, null, [], [], []);

    private IReadOnlyList<string> DetectInfrastructure(string repoPath)
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

        // CI detection
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
        if (!File.Exists(readmePath))
            return null;

        var content = TryReadFile(readmePath);
        if (content is null) return null;

        var words = content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', words.Take(MaxReadmeWords));
    }

    private string? TryReadFile(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Could not read {File}", path);
            return null;
        }
    }

    private static string FormatDotNetRuntime(string tfm)
    {
        if (tfm.StartsWith("net") && tfm.Length > 3 && char.IsDigit(tfm[3]))
            return $".NET {tfm[3..].Replace(".0", "")}";
        return tfm;
    }

    private static string? ExtractTargetFramework(string csprojContent)
    {
        var match = TargetFrameworkRegex().Match(csprojContent);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static bool IsDotNetTestFramework(string packageName) =>
        packageName is "xUnit" or "xunit" or "xunit.runner.visualstudio"
            or "NUnit" or "nunit" or "MSTest" or "MSTest.TestFramework"
            or "FluentAssertions" or "Moq" or "NSubstitute";

    private static IEnumerable<string> ExtractJsonDependencies(JsonElement root, string section)
    {
        if (!root.TryGetProperty(section, out var deps))
            yield break;
        foreach (var prop in deps.EnumerateObject())
            yield return prop.Name;
    }

    private static void DetectJsFramework(
        string repoPath, string pattern, string name, List<string> frameworks)
    {
        if (Directory.GetFiles(repoPath, pattern, SearchOption.TopDirectoryOnly).Length > 0)
            frameworks.Add(name);
    }

    private static string? DetectJsTestFramework(string repoPath)
    {
        if (Directory.GetFiles(repoPath, "vitest.config.*", SearchOption.TopDirectoryOnly).Length > 0)
            return "vitest";
        if (Directory.GetFiles(repoPath, "jest.config.*", SearchOption.TopDirectoryOnly).Length > 0)
            return "jest";
        if (Directory.GetFiles(repoPath, "playwright.config.*", SearchOption.TopDirectoryOnly).Length > 0)
            return "playwright test";
        if (Directory.GetFiles(repoPath, "cypress.config.*", SearchOption.TopDirectoryOnly).Length > 0)
            return "cypress run";
        return null;
    }

    private static string DetectJsPackageManager(string repoPath)
    {
        if (File.Exists(Path.Combine(repoPath, "pnpm-lock.yaml")))
            return "pnpm";
        if (File.Exists(Path.Combine(repoPath, "yarn.lock")))
            return "yarn";
        if (File.Exists(Path.Combine(repoPath, "bun.lockb"))
            || File.Exists(Path.Combine(repoPath, "bun.lock")))
            return "bun";
        if (File.Exists(Path.Combine(repoPath, "deno.json"))
            || File.Exists(Path.Combine(repoPath, "deno.lock")))
            return "deno";
        return "npm";
    }

    private static string DetectPythonPackageManager(string repoPath)
    {
        if (File.Exists(Path.Combine(repoPath, "uv.lock")))
            return "uv";
        if (File.Exists(Path.Combine(repoPath, "Pipfile")))
            return "pipenv";
        if (File.Exists(Path.Combine(repoPath, "requirements.txt")))
            return "pip";
        return "pip";
    }

    private static string DetectPythonTestCommand(string repoPath)
    {
        if (File.Exists(Path.Combine(repoPath, "tox.ini")))
            return "tox";

        var makefile = Path.Combine(repoPath, "Makefile");
        if (File.Exists(makefile))
        {
            try
            {
                var content = File.ReadAllText(makefile);
                if (content.Contains("test:"))
                    return "make test";
            }
            catch (IOException) { }
        }

        return "pytest";
    }

    [GeneratedRegex("""<TargetFramework>([^<]+)</TargetFramework>""")]
    private static partial Regex TargetFrameworkRegex();

    [GeneratedRegex(""""PackageReference Include="([^"]+)"""")]
    private static partial Regex PackageReferenceRegex();

    private sealed record DetectionResult(
        string Language,
        string? Runtime,
        string? PackageManager,
        string? BuildCommand,
        string? TestCommand,
        IReadOnlyList<string> Frameworks,
        IReadOnlyList<string> KeyFiles,
        IReadOnlyList<string> Sdks);
}
