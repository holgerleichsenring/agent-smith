using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Specs;
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

    /// <summary>
    /// p0429: a healthy api-scan must satisfy the contract it ratified. A criterion
    /// naming a step this preset cannot answer would fail every clean run — the gate
    /// has to hold against the real chain, not just against a unit fixture.
    /// </summary>
    [Fact]
    public async Task ApiSecurityScan_HealthyRun_SatisfiesEveryRatifiedScanCriterion()
    {
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default));
        harness.ChatClient.EnqueueText("No findings.");

        var runner = new PipelineRunner(harness.Services);
        await runner.RunAsync("api-security-scan");

        var pipeline = runner.LastContext!;
        var criteria = AcceptanceCriteria.For(pipeline);
        criteria.Should().NotBeEmpty();

        var verdict = RunDeliveryGate.Evaluate(RunAccountLedger.Current(pipeline), criteria.Count);
        verdict.Satisfied.Should().BeTrue(
            $"a clean api-scan has answered every target it stated. Outstanding: {verdict.FailureReason}");
    }
}
