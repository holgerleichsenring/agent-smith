using System.Text.RegularExpressions;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Core.Services.Detection;

public sealed partial class DotNetLanguageDetector(
    ILogger<DotNetLanguageDetector> logger) : ILanguageDetector
{
    public LanguageDetectionResult? Detect(string repoPath)
    {
        var slnFiles = Directory.GetFiles(repoPath, "*.sln", SearchOption.TopDirectoryOnly);
        var csprojFiles = Directory.GetFiles(repoPath, "*.csproj", SearchOption.AllDirectories);

        if (slnFiles.Length == 0 && csprojFiles.Length == 0)
            return null;

        var keyFiles = csprojFiles
            .Select(f => Path.GetRelativePath(repoPath, f))
            .ToList();

        if (File.Exists(Path.Combine(repoPath, "global.json")))
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

    private string? TryReadFile(string path)
    {
        try { return File.ReadAllText(path); }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Could not read {File}", path);
            return null;
        }
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
