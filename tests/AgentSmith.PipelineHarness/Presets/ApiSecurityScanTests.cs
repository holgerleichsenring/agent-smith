using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199 fast-tier api-security-scan coverage. The preset chains
/// LoadSwagger + Nuclei + Spectral + ZAP scanner spawns. p0199f moved the
/// scanner stubs into RealCompositionHarness defaults (env-gated by
/// AGENTSMITH_HARNESS_REAL_SCANNERS=1), so this test just asserts the
/// post-scanner chain (AgenticMaster + DeliverFindings) runs through.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class ApiSecurityScanTests
{
    [Fact]
    public async Task ApiSecurityScan_RealHandlerChainWithStubbedScanners_PipelineGreen()
    {
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default));
        harness.ChatClient.EnqueueText("No findings.");

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("api-security-scan");

        result.IsSuccess.Should().BeTrue($"api-security-scan must complete: {result.Message}");
    }
}
