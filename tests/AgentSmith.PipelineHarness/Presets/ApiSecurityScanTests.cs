using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
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

    /// <summary>
    /// p0429a: the substantiation step now runs in this preset, and the one thing it must
    /// never do is make the scan quieter. p0428 shipped a check as a gate and the harness
    /// refused fourteen healthy runs — so the endpoint check is proved against a real
    /// preset run, not only against a unit fixture.
    /// </summary>
    [Fact]
    public async Task ApiSecurityScan_HealthyRun_LosesNoLiveTargetFinding()
    {
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default));
        // The master's own triage, in the shape CollectMasterFindings parses: a live-target
        // finding citing an endpoint, with no OpenAPI document loaded behind it.
        harness.ChatClient.EnqueueText(
            """
            [{"description": "the endpoint answers anonymously", "suggestion": "require auth",
              "severity": "critical", "concern": "Security", "confidence": 90,
              "api_path": "/orders/{id}", "blocking": true}]
            """);

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("api-security-scan");

        result.IsSuccess.Should().BeTrue($"api-security-scan must complete: {result.Message}");
        var pipeline = runner.LastContext!;
        pipeline.TryGet<List<SkillObservation>>(ContextKeys.SkillObservations, out var delivered);
        (delivered ?? []).Should().OnlyContain(o => o.Severity == ObservationSeverity.Critical,
            "with no specification loaded the endpoint check answers nothing, so every "
            + "finding ships exactly as the master reported it");
    }
}
