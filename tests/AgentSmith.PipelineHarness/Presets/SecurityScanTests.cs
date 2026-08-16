using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
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

    /// <summary>
    /// p0429: the gate proved against a REAL preset run, not only against unit tests.
    /// A contract naming a step the preset cannot actually answer would fail every
    /// healthy scan — which is worse than no gate at all.
    /// </summary>
    [Fact]
    public async Task SecurityScan_HealthyRun_SatisfiesEveryRatifiedScanCriterion()
    {
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default));
        harness.ChatClient.EnqueueText("No new findings.");

        var runner = new PipelineRunner(harness.Services);
        await runner.RunAsync("security-scan");

        var pipeline = runner.LastContext!;
        var criteria = AcceptanceCriteria.For(pipeline);
        criteria.Should().NotBeEmpty("the scan states what it looked for before it looks");

        var verdict = RunDeliveryGate.Evaluate(RunAccountLedger.Current(pipeline), criteria.Count);
        verdict.Satisfied.Should().BeTrue(
            "a healthy scan must satisfy its own contract — every criterion names a step "
            + $"this preset really runs. Outstanding: {verdict.FailureReason}");
    }

    /// <summary>
    /// p0429: a healthy scan's findings all survive the substantiation step. The harness
    /// stands the refuter down, which is the production case that must never make a scan
    /// quieter — nothing the merge delivered may go missing.
    /// </summary>
    [Fact]
    public async Task SecurityScan_RefuterStoodDown_DeliversWhatTheMergeDelivered()
    {
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default));
        harness.ChatClient.EnqueueText("No new findings.");

        var runner = new PipelineRunner(harness.Services);
        await runner.RunAsync("security-scan");

        var pipeline = runner.LastContext!;
        pipeline.TryGet<List<SkillObservation>>(ContextKeys.UnvouchedFindings, out var promoted);
        pipeline.TryGet<List<SkillObservation>>(ContextKeys.SkillObservations, out var obs);
        var unvouched = promoted ?? new List<SkillObservation>();
        var delivered = obs ?? new List<SkillObservation>();

        unvouched.Should().OnlyContain(o => delivered.Contains(o),
            "silence is not a verdict: a refuter that cannot be asked leaves the scan "
            + "exactly as loud as the scanners made it");
    }
}
