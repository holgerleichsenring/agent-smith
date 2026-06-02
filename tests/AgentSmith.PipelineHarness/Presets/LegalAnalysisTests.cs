using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199 fast-tier legal-analysis coverage. Preset shape: AcquireSource
/// (copies the seeded SourceFilePath into the sandbox) → BootstrapDocument
/// (the markdown synthesis) → AgenticMaster (legal-analyst-master) →
/// DeliverOutput. Test scripts the master to write a findings markdown so
/// DeliverOutput has non-empty CodeChanges to publish.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class LegalAnalysisTests
{
    [Fact]
    public async Task LegalAnalysis_RealHandlerChain_PipelineGreen()
    {
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default));
        // BootstrapDocument's contract-classifier consumes the first
        // scripted response (Scout task, no tools); master takes the
        // remainder. Tests must understand the per-preset call order
        // and seed the queue accordingly — that's the production
        // surface shape we're pinning.
        harness.ChatClient
            .EnqueueText("nda")
            .EnqueueToolCall("write_file",
                """{"path":"primary/output/legal-findings.md","content":"# Findings"}""")
            .EnqueueText("Analysis complete.");

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("legal-analysis");

        result.IsSuccess.Should().BeTrue($"legal-analysis must complete: {result.Message}");
        harness.ChatClient.ToolCalls.ShouldHaveCalledInOrder("write_file");
    }
}
