using System.Text.RegularExpressions;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-08-25-3804: the convention table mapped this project's OWN language to an SDK one
/// major behind what this project targets — an SDK that cannot build it at all. It was
/// invisible because the failure lands inside a sandbox as a build error, where it reads as
/// the run's problem. The table's comment said "the latest SDK, a strictly safer default",
/// which was true when written and false a year later; a claim about the outside world either
/// gets a check or stops being made. This is the check.
/// </summary>
public sealed class ToolchainImageCatalogTests
{
    [Fact]
    public void Fallback_ForThisRepositorysLanguage_ResolvesAnImageThatCanBuildIt()
    {
        // The two facts are read from the repository itself, so neither can drift unnoticed:
        // what it declares it is written in, and what it actually compiles against.
        var declared = DeclaredLanguage();
        var target = HighestTargetFramework();

        var image = new SandboxImageChain().Resolve(new ResolvedProject(), declared, contextImage: null);

        SdkMajor(image).Should().BeGreaterThanOrEqualTo(target,
            $"this repository declares lang '{declared}' and targets net{target}.0, and a .NET SDK "
            + $"builds every TFM up to its own and none above it — '{image}' cannot build it");
    }

    [Fact]
    public void Fallback_EveryLanguageEntry_ResolvesToATrustedRegistry()
    {
        // These are OUR curated values. An entry an operator's default trust would refuse is a
        // run that dies at image pull, for a reason nobody named in this table can see.
        var trust = new ImageRegistryTrust();

        foreach (var (language, image) in ToolchainImageCatalog.KnownLanguages)
            trust.Accepts(image).Should().BeTrue($"'{language}' resolves to '{image}'");
    }

    [Fact]
    public void Fallback_AVersionedName_StillPinsWhatItNames()
    {
        // An unversioned name follows the newest entry; a versioned one is the operator saying
        // which. Moving the first must never quietly move the second.
        ToolchainImageCatalog.ForLanguage(".net 8").Should().Be("mcr.microsoft.com/dotnet/sdk:8.0");
        ToolchainImageCatalog.ForLanguage("dotnet9").Should().Be("mcr.microsoft.com/dotnet/sdk:9.0");
        ToolchainImageCatalog.ForLanguage("net8.0").Should().Be("mcr.microsoft.com/dotnet/sdk:8.0");
    }

    private static string DeclaredLanguage()
    {
        var yaml = File.ReadAllText(
            Path.Combine(RepoRoot(), ".agentsmith", "contexts", "default", "context.yaml"));
        var match = Regex.Match(yaml, @"^\s+lang:\s*(?<lang>\S.*?)\s*$", RegexOptions.Multiline);
        match.Success.Should().BeTrue("the default context must declare a language");
        return match.Groups["lang"].Value;
    }

    private static int HighestTargetFramework() =>
        Directory.EnumerateFiles(
                Path.Combine(RepoRoot(), "src", "backend"), "*.csproj", SearchOption.AllDirectories)
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"<TargetFramework>net(?<major>\d+)\.0<"))
            .Select(m => int.Parse(m.Groups["major"].Value))
            .DefaultIfEmpty(0)
            .Max();

    private static int SdkMajor(string image)
    {
        var match = Regex.Match(image, @"/sdk:(?<major>\d+)\.");
        match.Success.Should().BeTrue($"'{image}' is not a .NET SDK image, so it builds no C# at all");
        return int.Parse(match.Groups["major"].Value);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "dashboard")))
            dir = dir.Parent;
        dir.Should().NotBeNull("the test must find the repository root");
        return dir!.FullName;
    }
}
