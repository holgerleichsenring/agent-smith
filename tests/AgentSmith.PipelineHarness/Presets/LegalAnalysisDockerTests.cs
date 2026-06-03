using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;
using Xunit.Abstractions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199e docker-tier coverage for legal-analysis. Three end-to-end
/// assertions against the production DockerSandboxFactory + per-test bare
/// git remote + python:3.12-bookworm toolchain (resolved from the legal
/// fixture's `stack.lang: python`):
///   A: pip install markitdown succeeds, pipeline reaches DeliverOutput
///   B: BootstrapDocument produces non-empty markdown the master can act on
///   C: no install_command -> InstallDependencies skip, BootstrapDocument
///      fails when markitdown is absent (install step is the gate)
///
/// Each test runs only when AGENTSMITH_HARNESS_DOCKER=1 AND the docker
/// daemon is reachable; either gate failing produces a loud Skip log per
/// the harness convention. CI sets neither, so CI never picks the heavy
/// tier up.
/// </summary>
[Trait("Category", "PipelineHarness")]
[Trait("Tier", "Docker")]
public sealed class LegalAnalysisDockerTests(ITestOutputHelper output)
{
    private readonly DockerPresetHarness _harness = new(output);

    [Fact]
    public async Task Docker_LegalAnalysis_InstallDependencies_PipMarkitdownAvailable_PipelineGreen()
    {
        if (_harness.SkipIfUnavailable()) return;
        await using var run = await _harness.StartAsync("legal-analysis");
        var result = await run.Runner.RunAsync("legal-analysis");
        _harness.LogResult(result);

        result.IsSuccess.Should().BeTrue(
            $"pip install markitdown==<pin> from the fixture context.yaml " +
            $"must let BootstrapDocument shell out successfully: {result.Message}");
        run.Harness.DockerSandboxFactory!.Spawned.Should().NotBeEmpty(
            "at least one container spawned");
    }

    [Fact]
    public async Task Docker_LegalAnalysis_BootstrapDocumentExtractsMarkdown_DeliverOutputWritesFindings()
    {
        if (_harness.SkipIfUnavailable()) return;
        await using var run = await _harness.StartAsync("legal-analysis");
        var result = await run.Runner.RunAsync("legal-analysis");
        _harness.LogResult(result);

        result.IsSuccess.Should().BeTrue(
            $"happy-path orchestration (AcquireSource -> InstallDependencies -> " +
            $"BootstrapDocument -> AgenticMaster -> DeliverOutput) must complete: {result.Message}");
        run.Harness.ChatClient.ToolCalls.ShouldHaveCalledInOrder("write_file");
    }

    [Fact]
    public async Task Docker_LegalAnalysis_NoInstallCommand_StepSkipsCleanly_BootstrapDocumentFails()
    {
        if (_harness.SkipIfUnavailable()) return;
        await using var run = await _harness.StartAsync(
            "legal-analysis", SkillsBackend.Stub, NoInstallLayout());
        var result = await run.Runner.RunAsync("legal-analysis");
        _harness.LogResult(result);

        result.IsSuccess.Should().BeFalse(
            "without ci.install_command markitdown is absent on the clean " +
            "python image; BootstrapDocument's shell-out must fail and prove " +
            "the install step is the gate");
    }

    private static DockerPresetLayout NoInstallLayout() => new(
        FixturePaths.DockerLegal,
        FixturePaths.LegalFixtureNoInstallSource(),
        Path.Combine(FixturePaths.LegalFixtureNoInstallSource(), "inbox", "sample.txt"));
}
