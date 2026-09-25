using FluentAssertions;
using Xunit.Abstractions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199c docker-tier coverage for the fix-without-tests scenario: a trivial fix
/// where running the verify gate is overhead. It runs the <c>code</c> preset like
/// every other coding scenario and is told apart by the script it answers the master
/// with. Real dotnet restore happens in the BootstrapCheck + CheckoutSource flow; the
/// master writes one file and the pipeline closes with CommitAndPR persisting the WIP
/// branch to the bare remote.
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
        await using var run = await _harness.StartAsync(
            "code", DockerPresetScripts.FixWithoutTests);

        var result = await run.Runner.RunAsync("code");
        _harness.LogResult(result);

        result.IsSuccess.Should().BeTrue(
            $"a fix without tests must complete end-to-end in docker: {result.Message}");
        run.Harness.DockerSandboxFactory!.Spawned.Should().NotBeEmpty(
            "at least one sandbox container must have spawned");
    }
}
