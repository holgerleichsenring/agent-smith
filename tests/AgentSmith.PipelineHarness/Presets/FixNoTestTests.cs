using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199 fast-tier coverage for the fix-without-tests scenario: the same shape as
/// fixing a bug, minus the Test step. It runs the `code` preset like every other
/// coding scenario. The master writes a file; the harness asserts the preset runs
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
            // p0393: this scenario was the preset WITHOUT expectation negotiation; it now
            // runs `code`, which keeps that step until p0393a gives every run a spec.
            .EnqueueToolCall("write_file", """{"path":"primary/src/Quick.cs","content":"// quick fix"}""")
            .EnqueueText("Done.");

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("code");

        result.Should().NotBeNull("the pipeline must run to a terminal result, not throw");
        harness.ChatClient.ToolCalls.ShouldHaveCalledInOrder("write_file");
        harness.ChatClient.ToolCalls.First("write_file").StringArg("path")
            .Should().EndWith(".cs");
    }

    [Fact]
    public async Task FixNoTest_MasterReturnsZeroChanges_FailsKeystone()
    {
        // p0241: this scenario skips the test gate, but it still runs a code-changing
        // preset — a run that ships nothing is a failure, not a hollow success.
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default));
        harness.ChatClient
            // Call 1 is AnalyzeCode's project analyzer — this harness does not stub it.
            .EnqueueText("{}")
            .EnqueueText("No changes needed.");

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("code");

        // 2026-11-19-6b0ba: the stand-in carries the ticket's own criteria, and a master that ends without
        // finish_spec reported no outcome for them — the run delivered nothing and says so.
        result.IsSuccess.Should().BeFalse("a code run that ships nothing is a failure, not a hollow success");
        result.Message.Should().Contain("finish_spec");
    }
}
