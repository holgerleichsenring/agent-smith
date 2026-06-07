using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199 fast-tier fix-no-test coverage. Same shape as fix-bug minus the
/// Test step. The master writes a file; harness asserts the preset runs
/// end-to-end through the real composition.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class FixNoTestTests
{
    [Fact]
    public async Task FixNoTest_MasterToolCalls_RoundTripThroughComposition()
    {
        // Round-trips the write_file shape through the real composition. The
        // keystone-success path (real shipped change) is a Docker-tier concern —
        // the fast-tier StubSandbox cannot land a write into CodeChanges; p0239
        // hardens the fast tier to close that gap.
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default));
        harness.ChatClient
            .EnqueueToolCall("write_file", """{"path":"primary/src/Quick.cs","content":"// quick fix"}""")
            .EnqueueText("Done.");

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("fix-no-test");

        result.Should().NotBeNull("the pipeline must run to a terminal result, not throw");
        harness.ChatClient.ToolCalls.ShouldHaveCalledInOrder("write_file");
        harness.ChatClient.ToolCalls.First("write_file").StringArg("path")
            .Should().EndWith(".cs");
    }

    [Fact]
    public async Task FixNoTest_MasterReturnsZeroChanges_FailsKeystone()
    {
        // p0241: fix-no-test skips the test gate, but it is still a code-changing
        // preset — a run that ships nothing is a failure, not a hollow success.
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default));
        harness.ChatClient.EnqueueText("No changes needed.");

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("fix-no-test");

        result.IsSuccess.Should().BeFalse("a fix-no-test that changed no source must not be a success");
        result.Message.Should().Contain("no code changes");
    }
}
