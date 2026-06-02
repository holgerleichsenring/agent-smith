using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199 fast-tier mad-discussion coverage. The preset's distinguishing
/// step is the agentic master that internally spawns the five MAD
/// perspectives. We don't script that internal orchestration — the
/// scripted master simply emits a write_file (the consolidated
/// discussion artefact) and ends. CommitAndPR's tolerance for empty
/// CodeChanges and the Discussion-pipeline shape are what this test
/// pins through the real composition.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class MadDiscussionTests
{
    [Fact]
    public async Task MadDiscussion_MasterWritesDiscussionMarkdown_PipelineGreen()
    {
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default));
        harness.ChatClient
            .EnqueueToolCall("write_file",
                """{"path":"primary/discussions/mad-discussion.md","content":"# Synthesis"}""")
            .EnqueueText("Discussion synthesised.");

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("mad-discussion");

        result.IsSuccess.Should().BeTrue($"mad-discussion must complete: {result.Message}");
        harness.ChatClient.ToolCalls.ShouldHaveCalledInOrder("write_file");
        harness.ChatClient.ToolCalls.First("write_file").StringArg("path")
            .Should().EndWith(".md");
    }

    [Fact]
    public async Task MadDiscussion_MasterReturnsZeroChanges_PipelineGreen()
    {
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default));
        harness.ChatClient.EnqueueText("Nothing to discuss.");

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("mad-discussion");

        result.IsSuccess.Should().BeTrue($"empty-master path must stay green: {result.Message}");
    }
}
