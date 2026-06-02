using FluentAssertions;
using Xunit.Abstractions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199c docker-tier security-scan coverage. Proves the three scanners
/// (StaticPatternScan + GitHistoryScan + DependencyAudit) and SecurityTrend
/// all execute inside the production DockerSandbox over the real bare-repo
/// working tree, and that AgenticMaster + DeliverFindings publish the
/// consolidated artefact through the container's filesystem.
/// </summary>
[Trait("Category", "PipelineHarness")]
[Trait("Tier", "Docker")]
public sealed class SecurityScanDockerTests(ITestOutputHelper output)
{
    private readonly DockerPresetHarness _harness = new(output);

    [Fact]
    public async Task Docker_SecurityScan_GreenPath_PipelineSucceeds()
    {
        if (_harness.SkipIfUnavailable()) return;
        await using var run = await _harness.StartAsync("security-scan");

        var result = await run.Runner.RunAsync("security-scan");
        _harness.LogResult(result);

        result.IsSuccess.Should().BeTrue(
            $"security-scan must complete in docker: {result.Message}");
        run.Harness.DockerSandboxFactory!.Spawned.Should().NotBeEmpty(
            "scanners run inside the spawned sandbox container");
    }
}
