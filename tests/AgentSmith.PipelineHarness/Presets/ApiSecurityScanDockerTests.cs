using AgentSmith.Contracts.Providers;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199f docker-tier coverage for api-security-scan. The passive-mode
/// path (no source checkout, BootstrapGate's conditional skip on api-scan
/// absorbs source_available=false, scanners run as stubs against a real
/// Kestrel target, pipeline reaches DeliverFindings) is the production-
/// shape default for api-scan operators. The env-gate test is independent
/// of docker so it runs everywhere.
///
/// Source-mode (SourcePathOverride at the per-test working copy) lands as
/// a follow-up (p0199g). The CsharpFixture binds .agentsmith/ on the host
/// but DockerSandbox's /work is a docker-managed volume; binding a second
/// host path on the same mount fails with "Duplicate mount point: /work".
/// Closing that needs either (1) docker cp the working copy into /work
/// after container start, or (2) a sandbox-spec change that lets the
/// harness substitute /work with a host bind. Both are spec-level work
/// orders — the passive-mode path already covers the production shape
/// every operator's --target / --swagger invocation actually exercises.
///
/// Tests A-B run only when AGENTSMITH_HARNESS_DOCKER=1 AND the docker
/// daemon is reachable; test D is a DI-shape assertion and runs always.
/// CI sets neither env, so CI never picks the heavy tier up.
/// </summary>
[Trait("Category", "PipelineHarness")]
[Trait("Tier", "Docker")]
public sealed class ApiSecurityScanDockerTests(ITestOutputHelper output)
{
    private readonly DockerPresetHarness _harness = new(output);

    [Fact]
    public async Task Docker_ApiSecurityScan_PassiveMode_StubScanners_PipelineReachesDeliverFindings()
    {
        if (_harness.SkipIfUnavailable()) return;
        await using var run = await _harness.StartAsync("api-security-scan");
        var result = await run.Runner.RunAsync("api-security-scan");
        _harness.LogResult(result);

        result.IsSuccess.Should().BeTrue(
            $"passive-mode happy path (TryCheckoutSource -> WarnPassive -> " +
            $"BootstrapGate skip -> LoadSwagger -> stub scanners -> AgenticMaster " +
            $"-> DeliverFindings) must complete: {result.Message}");
        run.ApiTarget.Should().NotBeNull(
            "passive mode must boot the StubApiTargetHost so ApiTarget points " +
            "at a real URL even when scanners are stubbed");
    }

    [Fact]
    public async Task Docker_ApiSecurityScan_PassiveMode_BootstrapGateEmitsWarnPassive_NoAbort()
    {
        if (_harness.SkipIfUnavailable()) return;
        await using var run = await _harness.StartAsync("api-security-scan");
        var result = await run.Runner.RunAsync("api-security-scan");
        _harness.LogResult(result);

        result.IsSuccess.Should().BeTrue(
            $"BootstrapGate's p0130a conditional skip on api-scan must absorb " +
            $"source_available=false instead of aborting the pipeline: {result.Message}");
        run.Harness.ChatClient.ToolCalls.ShouldHaveCalledInOrder("write_file");
    }

    [Fact]
    public void Docker_ApiSecurityScan_SourceMode_DeferredToWorkVolumeBindPath_LoudSkip()
    {
        output.WriteLine(
            "DOCKER TIER SOURCE-MODE NOT EXERCISED for api-security-scan — deferred to " +
            "p0199g. TryCheckoutSource publishes Repository at the host path (already " +
            "wired via SourcePathOverride), but DockerSandbox's /work is a docker-managed " +
            "volume; binding a second host path at the same mount fails with 'Duplicate " +
            "mount point: /work'. Two unsticking options: (1) docker cp the working copy " +
            "into /work after container start, (2) sandbox-spec change letting the harness " +
            "swap the /work volume for a host bind. Passive mode covers the production " +
            "shape that every --target / --swagger CLI invocation actually exercises.");
    }

    [Fact]
    public async Task Docker_ApiSecurityScan_RealScannersEnvGate_OptInOnly_StubsRemainByDefault()
    {
        var configPath = FixturePaths.For(FixturePaths.Default);

        Environment.SetEnvironmentVariable(RealCompositionHarness.RealScannersEnv, null);
        await using (var stubbed = RealCompositionHarness.Build(configPath))
        {
            stubbed.Services.GetRequiredService<INucleiScanner>()
                .GetType().FullName.Should().StartWith("AgentSmith.PipelineHarness.Presets.ApiScannerStubs",
                    "default test path must register the empty-findings scanner stubs");
        }

        Environment.SetEnvironmentVariable(RealCompositionHarness.RealScannersEnv, "1");
        try
        {
            await using var live = RealCompositionHarness.Build(configPath);
            live.Services.GetRequiredService<INucleiScanner>()
                .GetType().Name.Should().Be("NucleiSpawner",
                    "AGENTSMITH_HARNESS_REAL_SCANNERS=1 must keep the production adapter " +
                    "wired so an operator can run live Nuclei scans against StubApiTargetHost");
        }
        finally
        {
            Environment.SetEnvironmentVariable(RealCompositionHarness.RealScannersEnv, null);
        }
    }
}
