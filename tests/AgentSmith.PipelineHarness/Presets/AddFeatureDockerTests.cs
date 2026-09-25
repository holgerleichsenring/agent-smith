using FluentAssertions;
using Xunit.Abstractions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199c docker-tier coverage for the add-a-feature scenario, which differs from
/// fixing a bug in its post-master chain: GenerateTests + Test + GenerateDocs all run
/// after AgenticMaster. Both scenarios run the <c>code</c> preset; the script is what
/// tells them apart. This test proves the chain completes in the real DockerSandbox
/// (so any composition-root regression in the post-master handler set surfaces here)
/// and that the WIP branch reaches the fake remote.
/// </summary>
[Trait("Category", "PipelineHarness")]
[Trait("Tier", "Docker")]
public sealed class AddFeatureDockerTests(ITestOutputHelper output)
{
    private readonly DockerPresetHarness _harness = new(output);

    [Fact]
    public async Task Docker_AddFeature_GreenPath_PipelineSucceeds()
    {
        if (_harness.SkipIfUnavailable()) return;
        await using var run = await _harness.StartAsync(
            "code", DockerPresetScripts.NewFeature);

        var result = await run.Runner.RunAsync("code");
        _harness.LogResult(result);
        output.WriteLine("bare branches: " + string.Join(", ", run.Session.BareBranches()));

        result.IsSuccess.Should().BeTrue(
            $"adding a feature must complete with GenerateTests + Test + GenerateDocs in docker: {result.Message}");
        run.Harness.DockerSandboxFactory!.Spawned.Should().NotBeEmpty();
    }
}
