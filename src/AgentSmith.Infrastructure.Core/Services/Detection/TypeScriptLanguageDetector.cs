using System.Text.Json;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Core.Services.Detection;

public sealed class TypeScriptLanguageDetector(
    ILogger<TypeScriptLanguageDetector> logger) : ILanguageDetector
{
    public LanguageDetectionResult? Detect(string repoPath)
    {
        var packageJsonPath = Path.Combine(repoPath, "package.json");
        var denoJsonPath = Path.Combine(repoPath, "deno.json");

        if (!File.Exists(packageJsonPath) && !File.Exists(denoJsonPath))
            return null;

        var keyFiles = new List<string>();
        var sdks = new List<string>();
        var frameworks = new List<string>();
        string? buildCmd = null;
        string? testCmd = null;
        var language = File.Exists(Path.Combine(repoPath, "tsconfig.json"))
            ? "TypeScript" : "JavaScript";

        if (File.Exists(packageJsonPath))
        {
            keyFiles.Add("package.json");
            if (File.Exists(Path.Combine(repoPath, "tsconfig.json")))
                keyFiles.Add("tsconfig.json");

            ParsePackageJson(packageJsonPath, sdks, ref buildCmd, ref testCmd);
        }

        DetectFrameworks(repoPath, frameworks);
        testCmd ??= DetectTestFramework(repoPath);
        var packageManager = DetectPackageManager(repoPath);

        if (packageManager is "pnpm" or "yarn" or "bun")
        {
            buildCmd = buildCmd?.Replace("npm run build", $"{packageManager} run build");
            testCmd = testCmd?.Replace("npm test", $"{packageManager} test");
        }

        var runtime = File.Exists(denoJsonPath) ? "Deno" : "Node.js";

        return new LanguageDetectionResult(
            Language: language,
            Runtime: runtime,
            PackageManager: packageManager,
            BuildCommand: buildCmd,
            TestCommand: testCmd,
            Frameworks: frameworks,
            KeyFiles: keyFiles,
            Sdks: sdks.Distinct().ToList());
    }

    private void ParsePackageJson(
        string path, List<string> sdks, ref string? buildCmd, ref string? testCmd)
    {
        try
        {
            var content = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            if (root.TryGetProperty("scripts", out var scripts))
            {
                if (scripts.TryGetProperty("build", out _)) buildCmd = "npm run build";
                if (scripts.TryGetProperty("test", out _)) testCmd = "npm test";
            }

            ExtractDeps(root, "dependencies", sdks);
            ExtractDeps(root, "devDependencies", sdks);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            logger.LogWarning(ex, "Failed to parse package.json");
        }
    }

    private static void ExtractDeps(JsonElement root, string section, List<string> sdks)
    {
        if (!root.TryGetProperty(section, out var deps)) return;
        foreach (var prop in deps.EnumerateObject())
            sdks.Add(prop.Name);
    }

    private static void DetectFrameworks(string repoPath, List<string> frameworks)
    {
        CheckConfig(repoPath, "next.config.*", "Next.js", frameworks);
        CheckConfig(repoPath, "angular.json", "Angular", frameworks);
        CheckConfig(repoPath, "vite.config.*", "Vite", frameworks);
        CheckConfig(repoPath, "nuxt.config.*", "Nuxt", frameworks);
        CheckConfig(repoPath, "svelte.config.*", "SvelteKit", frameworks);
        CheckConfig(repoPath, "remix.config.*", "Remix", frameworks);
        CheckConfig(repoPath, "astro.config.*", "Astro", frameworks);
    }

    private static void CheckConfig(
        string repoPath, string pattern, string name, List<string> frameworks)
    {
        if (Directory.GetFiles(repoPath, pattern, SearchOption.TopDirectoryOnly).Length > 0)
            frameworks.Add(name);
    }

    private static string? DetectTestFramework(string repoPath)
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

    private static string DetectPackageManager(string repoPath)
    {
        if (File.Exists(Path.Combine(repoPath, "pnpm-lock.yaml"))) return "pnpm";
        if (File.Exists(Path.Combine(repoPath, "yarn.lock"))) return "yarn";
        if (File.Exists(Path.Combine(repoPath, "bun.lockb"))
            || File.Exists(Path.Combine(repoPath, "bun.lock"))) return "bun";
        if (File.Exists(Path.Combine(repoPath, "deno.json"))
            || File.Exists(Path.Combine(repoPath, "deno.lock"))) return "deno";
        return "npm";
    }
}
