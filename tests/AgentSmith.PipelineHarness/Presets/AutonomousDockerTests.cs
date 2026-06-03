using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;
using Xunit.Abstractions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199d docker-tier autonomous coverage. Opts into SkillsBackend.Fixture
/// so DeterministicTriageSelector matches autonomous-planner + autonomous-
/// investigator from the checked-in fixture catalog and Triage emits
/// non-empty Plan-phase commands. Asserts the end-to-end chain
/// (PipelineNameInitializer through WriteRunResult) stays green under the
/// production DockerSandbox.
/// </summary>
[Trait("Category", "PipelineHarness")]
[Trait("Tier", "Docker")]
public sealed class AutonomousDockerTests(ITestOutputHelper output)
{
    private readonly DockerPresetHarness _harness = new(output);

    [Fact]
    public async Task Docker_Autonomous_TriageStrategyResolvesNonEmptyRoles_PipelineGreen()
    {
        if (_harness.SkipIfUnavailable()) return;
        await using var run = await _harness.StartAsync("autonomous", SkillsBackend.Fixture);

        var result = await run.Runner.RunAsync("autonomous");
        _harness.LogResult(result);

        result.IsSuccess.Should().BeTrue(
            $"autonomous must complete end-to-end in docker with the fixture skill catalog: {result.Message}");
        run.Harness.DockerSandboxFactory!.Spawned.Should().NotBeEmpty(
            "at least one sandbox container must have spawned");
    }
}
