using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199 fast-tier add-feature coverage. add-feature's distinguishing
/// shape is GenerateTests + GenerateDocs after the agentic master. Test
/// scripts a write_file + run_command sequence; harness asserts the
/// preset completes the full chain (master + post-master handlers).
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class AddFeatureTests
{
    [Fact]
    public async Task AddFeature_MasterWritesFeatureAndRunsTests_PipelineGreen()
    {
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default));
        harness.ChatClient
            .EnqueueToolCall("write_file", """{"path":"primary/src/Feature.cs","content":"public class Feature {}"}""")
            .EnqueueToolCall("run_command", """{"command":"dotnet test","repo":"primary"}""")
            .EnqueueText("Feature added; tests green.");

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("add-feature");

        result.IsSuccess.Should().BeTrue($"add-feature must complete: {result.Message}");
        harness.ChatClient.ToolCalls.ShouldHaveCalledInOrder("write_file", "run_command");
        harness.ChatClient.ToolCalls.First("run_command").StringArg("command")
            .Should().Contain("dotnet");
    }

    [Fact]
    public async Task AddFeature_MasterReturnsZeroChanges_PipelineGreen()
    {
        // GenerateTests + GenerateDocs must tolerate an empty CodeChanges
        // list — the master may decide the feature is already implemented.
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default));
        harness.ChatClient.EnqueueText("Already implemented.");

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("add-feature");

        result.IsSuccess.Should().BeTrue($"empty-master path must stay green: {result.Message}");
    }
}
