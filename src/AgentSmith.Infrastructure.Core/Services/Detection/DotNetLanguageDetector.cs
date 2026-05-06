using System.Text.RegularExpressions;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Core.Services.Detection;

public sealed partial class DotNetLanguageDetector(
    ILogger<DotNetLanguageDetector> logger) : ILanguageDetector
{
    public async Task<LanguageDetectionResult?> DetectAsync(
        ISandboxFileReader reader, string repoPath, CancellationToken cancellationToken)
    {
        var topEntries = await reader.ListAsync(repoPath, maxDepth: 1, cancellationToken);
        var slnFiles = topEntries.Where(e => e.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)).ToList();

        var allEntries = await reader.ListAsync(repoPath, maxDepth: 8, cancellationToken);
        var csprojFiles = allEntries
            .Where(e => e.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (slnFiles.Count == 0 && csprojFiles.Count == 0)
            return null;

        var keyFiles = csprojFiles
            .Select(f => RelativeOf(f, repoPath))
            .ToList();

        if (await reader.ExistsAsync(Path.Combine(repoPath, "global.json"), cancellationToken))
            keyFiles.Add("global.json");

        string? runtime = null;
        var sdks = new List<string>();
        var frameworks = new List<string>();
        var hasTestSdk = false;

        foreach (var csproj in csprojFiles)
        {
            var content = await TryReadAsync(reader, csproj, cancellationToken);
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

        frameworks.AddRange(sdks.Where(IsTestFramework).Distinct());

        return new LanguageDetectionResult(
            Language: "C#",
            Runtime: runtime is not null ? FormatRuntime(runtime) : ".NET",
            PackageManager: "NuGet",
            BuildCommand: "dotnet build",
            TestCommand: hasTestSdk ? "dotnet test" : null,
            Frameworks: frameworks.Distinct().ToList(),
            KeyFiles: keyFiles,
            Sdks: sdks.Distinct().ToList());
    }

    private async Task<string?> TryReadAsync(ISandboxFileReader reader, string path, CancellationToken ct)
    {
        try { return await reader.TryReadAsync(path, ct); }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not read {File}", path);
            return null;
        }
    }

    private static string RelativeOf(string fullPath, string repoPath)
    {
        var rel = fullPath.Length > repoPath.Length ? fullPath[repoPath.Length..] : fullPath;
        return rel.TrimStart('/');
    }

    private static string FormatRuntime(string tfm) =>
        tfm.StartsWith("net") && tfm.Length > 3 && char.IsDigit(tfm[3])
            ? $".NET {tfm[3..].Replace(".0", "")}"
            : tfm;

    private static string? ExtractTargetFramework(string csprojContent)
    {
        var match = TargetFrameworkRegex().Match(csprojContent);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static bool IsTestFramework(string packageName) =>
        packageName is "xUnit" or "xunit" or "xunit.runner.visualstudio"
            or "NUnit" or "nunit" or "MSTest" or "MSTest.TestFramework"
            or "FluentAssertions" or "Moq" or "NSubstitute";

    [GeneratedRegex("""<TargetFramework>([^<]+)</TargetFramework>""")]
    private static partial Regex TargetFrameworkRegex();

    [GeneratedRegex(""""PackageReference Include="([^"]+)"""")]
    private static partial Regex PackageReferenceRegex();
}
