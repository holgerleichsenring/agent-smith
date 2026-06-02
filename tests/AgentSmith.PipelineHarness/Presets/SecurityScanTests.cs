using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199 fast-tier security-scan coverage. Real handlers run the three
/// scanners (StaticPatternScan / GitHistoryScan / DependencyAudit) +
/// SecurityTrend over the stub sandbox; the master synthesises and
/// DeliverFindings writes the artefact. Test pins the preset round-
/// trip through the real composition; per-scanner content is exercised
/// by AgentSmith.Tests unit tests.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SecurityScanTests
{
    [Fact]
    public async Task SecurityScan_RealHandlerChain_PipelineGreen()
    {
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default));
        // Master emits a write_file for the consolidated findings artefact;
        // the chain's DeliverFindings handler tolerates either shape.
        harness.ChatClient
            .EnqueueToolCall("write_file",
                """{"path":"primary/.agentsmith/security/scan.md","content":"# Findings"}""")
            .EnqueueText("Scan synthesised.");

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("security-scan");

        result.IsSuccess.Should().BeTrue($"security-scan must complete: {result.Message}");
    }

    [Fact]
    public async Task SecurityScan_MasterReturnsZeroChanges_PipelineGreen()
    {
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default));
        harness.ChatClient.EnqueueText("No new findings.");

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("security-scan");

        result.IsSuccess.Should().BeTrue($"empty-master path must stay green: {result.Message}");
    }
}
