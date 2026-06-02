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
    public async Task FixNoTest_MasterWritesFile_PipelineGreen()
    {
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default));
        harness.ChatClient
            .EnqueueToolCall("write_file", """{"path":"primary/src/Quick.cs","content":"// quick fix"}""")
            .EnqueueText("Done.");

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("fix-no-test");

        result.IsSuccess.Should().BeTrue($"fix-no-test must complete: {result.Message}");
        harness.ChatClient.ToolCalls.ShouldHaveCalledInOrder("write_file");
        harness.ChatClient.ToolCalls.First("write_file").StringArg("path")
            .Should().EndWith(".cs");
    }

    [Fact]
    public async Task FixNoTest_MasterReturnsZeroChanges_PipelineGreen()
    {
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default));
        harness.ChatClient.EnqueueText("No changes needed.");

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("fix-no-test");

        result.IsSuccess.Should().BeTrue($"empty-master path must stay green: {result.Message}");
    }
}
