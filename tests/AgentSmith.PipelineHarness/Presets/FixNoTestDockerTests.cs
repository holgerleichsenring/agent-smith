using FluentAssertions;
using Xunit.Abstractions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199c docker-tier fix-no-test coverage. Proves the preset's distinguishing
/// shape (no Test step — the variant exists for trivial fixes where running
/// the verify gate is overhead) runs end-to-end through the production
/// DockerSandbox. Real dotnet restore happens in BootstrapCheck +
/// CheckoutSource flow; the master writes one file and the pipeline closes
/// with CommitAndPR persisting the WIP branch to the bare remote.
/// </summary>
[Trait("Category", "PipelineHarness")]
[Trait("Tier", "Docker")]
public sealed class FixNoTestDockerTests(ITestOutputHelper output)
{
    private readonly DockerPresetHarness _harness = new(output);

    [Fact]
    public async Task Docker_FixNoTest_GreenPath_PipelineSucceeds()
    {
        if (_harness.SkipIfUnavailable()) return;
        await using var run = await _harness.StartAsync("fix-no-test");

        var result = await run.Runner.RunAsync("fix-no-test");
        _harness.LogResult(result);

        result.IsSuccess.Should().BeTrue(
            $"fix-no-test must complete end-to-end in docker: {result.Message}");
        run.Harness.DockerSandboxFactory!.Spawned.Should().NotBeEmpty(
            "at least one sandbox container must have spawned");
    }
}
