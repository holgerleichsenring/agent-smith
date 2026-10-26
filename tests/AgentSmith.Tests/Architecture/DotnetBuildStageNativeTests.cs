using System.Text.RegularExpressions;
using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-10-26-9849: a .NET SDK stage built for a foreign platform runs under QEMU, which made the
/// arm64 leg of the publish workflow compile for 16 minutes against 2 natively. Every Dockerfile
/// under the backend runs its SDK stages on the build platform and targets the image's platform
/// through the output (portable IL, or a RID) instead.
/// </summary>
public sealed class DotnetBuildStageNativeTests
{
    private static readonly Regex SdkStage = new(
        @"^FROM\s+(?<platform>--platform=\S+\s+)?mcr\.microsoft\.com/dotnet/sdk:\S+\s+AS\s+(?<stage>\S+)",
        RegexOptions.Multiline | RegexOptions.IgnoreCase);

    // Stages that run what they fetched, so they must be the target platform's own.
    private static readonly HashSet<string> TargetPlatformStages = new(StringComparer.OrdinalIgnoreCase)
    {
        "python-fetch", "runtime",
    };

    public static TheoryData<string> Dockerfiles()
    {
        var data = new TheoryData<string>();
        foreach (var path in Directory.EnumerateFiles(ArchitectureSources.BackendRoot, "Dockerfile", SearchOption.AllDirectories))
            data.Add(Path.GetRelativePath(ArchitectureSources.RepositoryRoot, path));
        return data;
    }

    [Theory]
    [MemberData(nameof(Dockerfiles))]
    public void DotnetSdkBuildStage_RunsOnTheBuildPlatform(string dockerfile)
    {
        var text = File.ReadAllText(Path.Combine(ArchitectureSources.RepositoryRoot, dockerfile));
        var foreign = SdkStage.Matches(text)
            .Where(m => !TargetPlatformStages.Contains(m.Groups["stage"].Value))
            .Where(m => !m.Groups["platform"].Value.Contains("$BUILDPLATFORM", StringComparison.Ordinal))
            .Select(m => m.Groups["stage"].Value)
            .ToList();

        foreign.Should().BeEmpty(
            $"{dockerfile} compiles in these SDK stages for the target platform, which runs under "
            + "QEMU for arm64; start them with FROM --platform=$BUILDPLATFORM");
    }

    [Fact]
    public void DotnetSdkBuildStage_TheEnumeration_FindsTheThreePublishedImages()
    {
        Dockerfiles().Select(d => ((string)d[0]).Replace('\\', '/')).Should().Contain(
        [
            "src/backend/AgentSmith.Server/Dockerfile",
            "src/backend/AgentSmith.Cli/Dockerfile",
            "src/backend/AgentSmith.Sandbox.Agent/Dockerfile",
        ]);
    }
}
