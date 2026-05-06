using System.Text.Json;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Core.Services.Detection;

public sealed class TypeScriptLanguageDetector(
    ILogger<TypeScriptLanguageDetector> logger) : ILanguageDetector
{
    public async Task<LanguageDetectionResult?> DetectAsync(
        ISandboxFileReader reader, string repoPath, CancellationToken cancellationToken)
    {
        var packageJsonPath = Path.Combine(repoPath, "package.json");
        var denoJsonPath = Path.Combine(repoPath, "deno.json");

        var hasPackageJson = await reader.ExistsAsync(packageJsonPath, cancellationToken);
        var hasDenoJson = await reader.ExistsAsync(denoJsonPath, cancellationToken);
        if (!hasPackageJson && !hasDenoJson)
            return null;

        var keyFiles = new List<string>();
        var sdks = new List<string>();
        var frameworks = new List<string>();
        string? buildCmd = null;
        string? testCmd = null;
        var hasTsConfig = await reader.ExistsAsync(Path.Combine(repoPath, "tsconfig.json"), cancellationToken);
        var language = hasTsConfig ? "TypeScript" : "JavaScript";

        if (hasPackageJson)
        {
            keyFiles.Add("package.json");
            if (hasTsConfig) keyFiles.Add("tsconfig.json");
            await ParsePackageJsonAsync(reader, packageJsonPath, sdks, t => buildCmd = t, t => testCmd = t, cancellationToken);
        }

        var topEntries = await reader.ListAsync(repoPath, maxDepth: 1, cancellationToken);
        DetectFrameworks(topEntries, frameworks);
        testCmd ??= DetectTestFramework(topEntries);
        var packageManager = await DetectPackageManagerAsync(reader, repoPath, cancellationToken);

        if (packageManager is "pnpm" or "yarn" or "bun")
        {
            buildCmd = buildCmd?.Replace("npm run build", $"{packageManager} run build");
            testCmd = testCmd?.Replace("npm test", $"{packageManager} test");
        }

        var runtime = hasDenoJson ? "Deno" : "Node.js";

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

    private async Task ParsePackageJsonAsync(
        ISandboxFileReader reader, string path, List<string> sdks,
        Action<string> setBuild, Action<string> setTest, CancellationToken cancellationToken)
    {
        try
        {
            var content = await reader.TryReadAsync(path, cancellationToken);
            if (content is null) return;
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            if (root.TryGetProperty("scripts", out var scripts))
            {
                if (scripts.TryGetProperty("build", out _)) setBuild("npm run build");
                if (scripts.TryGetProperty("test", out _)) setTest("npm test");
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

    private static void DetectFrameworks(IReadOnlyList<string> topEntries, List<string> frameworks)
    {
        CheckPattern(topEntries, "next.config.", "Next.js", frameworks);
        CheckPattern(topEntries, "angular.json", "Angular", frameworks, exact: true);
        CheckPattern(topEntries, "vite.config.", "Vite", frameworks);
        CheckPattern(topEntries, "nuxt.config.", "Nuxt", frameworks);
        CheckPattern(topEntries, "svelte.config.", "SvelteKit", frameworks);
        CheckPattern(topEntries, "remix.config.", "Remix", frameworks);
        CheckPattern(topEntries, "astro.config.", "Astro", frameworks);
    }

    private static void CheckPattern(
        IReadOnlyList<string> entries, string namePrefix, string display,
        List<string> frameworks, bool exact = false)
    {
        var match = entries.Any(e =>
        {
            var name = LastSegment(e);
            return exact
                ? name.Equals(namePrefix, StringComparison.OrdinalIgnoreCase)
                : name.StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase);
        });
        if (match) frameworks.Add(display);
    }

    private static string? DetectTestFramework(IReadOnlyList<string> entries)
    {
        if (entries.Any(e => LastSegment(e).StartsWith("vitest.config.", StringComparison.OrdinalIgnoreCase)))
            return "vitest";
        if (entries.Any(e => LastSegment(e).StartsWith("jest.config.", StringComparison.OrdinalIgnoreCase)))
            return "jest";
        if (entries.Any(e => LastSegment(e).StartsWith("playwright.config.", StringComparison.OrdinalIgnoreCase)))
            return "playwright test";
        if (entries.Any(e => LastSegment(e).StartsWith("cypress.config.", StringComparison.OrdinalIgnoreCase)))
            return "cypress run";
        return null;
    }

    private static async Task<string> DetectPackageManagerAsync(
        ISandboxFileReader reader, string repoPath, CancellationToken cancellationToken)
    {
        if (await reader.ExistsAsync(Path.Combine(repoPath, "pnpm-lock.yaml"), cancellationToken)) return "pnpm";
        if (await reader.ExistsAsync(Path.Combine(repoPath, "yarn.lock"), cancellationToken)) return "yarn";
        if (await reader.ExistsAsync(Path.Combine(repoPath, "bun.lockb"), cancellationToken)
            || await reader.ExistsAsync(Path.Combine(repoPath, "bun.lock"), cancellationToken)) return "bun";
        if (await reader.ExistsAsync(Path.Combine(repoPath, "deno.json"), cancellationToken)
            || await reader.ExistsAsync(Path.Combine(repoPath, "deno.lock"), cancellationToken)) return "deno";
        return "npm";
    }

    private static string LastSegment(string path)
    {
        var idx = path.LastIndexOf('/');
        return idx < 0 ? path : path[(idx + 1)..];
    }
}
