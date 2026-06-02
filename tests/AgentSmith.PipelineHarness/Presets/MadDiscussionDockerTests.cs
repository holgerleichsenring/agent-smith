using FluentAssertions;
using Xunit.Abstractions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199c docker-tier mad-discussion coverage. The five MAD perspectives
/// (dreamer / realist / philosopher / devils-advocate / silencer) are
/// orchestrated INSIDE the agentic master via spawn_agents; the harness
/// scripts the master's outer envelope (one write_file for the synthesised
/// discussion markdown) and asserts the discussion artefact lands on the
/// remote via CommitAndPR.
/// </summary>
[Trait("Category", "PipelineHarness")]
[Trait("Tier", "Docker")]
public sealed class MadDiscussionDockerTests(ITestOutputHelper output)
{
    private readonly DockerPresetHarness _harness = new(output);

    [Fact]
    public async Task Docker_MadDiscussion_GreenPath_PipelineSucceeds()
    {
        if (_harness.SkipIfUnavailable()) return;
        await using var run = await _harness.StartAsync("mad-discussion");

        var result = await run.Runner.RunAsync("mad-discussion");
        _harness.LogResult(result);
        output.WriteLine("bare branches: " + string.Join(", ", run.Session.BareBranches()));

        result.IsSuccess.Should().BeTrue(
            $"mad-discussion must complete in docker: {result.Message}");
        run.Harness.DockerSandboxFactory!.Spawned.Should().NotBeEmpty();
    }
}
