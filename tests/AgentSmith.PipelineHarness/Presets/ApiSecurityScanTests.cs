using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199 fast-tier api-security-scan coverage. The preset chains
/// LoadSwagger + Nuclei + Spectral + ZAP scanner spawns. With docker not
/// guaranteed in the test environment, the real spawners would crash; we
/// stub each scanner to a no-op-empty-findings shape so the preset's
/// post-scanner chain (AgenticMaster + DeliverFindings) runs.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class ApiSecurityScanTests
{
    [Fact]
    public async Task ApiSecurityScan_RealHandlerChainWithStubbedScanners_PipelineGreen()
    {
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default),
            ApiScannerStubs.Register);
        harness.ChatClient.EnqueueText("No findings.");

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("api-security-scan");

        result.IsSuccess.Should().BeTrue($"api-security-scan must complete: {result.Message}");
    }
}
