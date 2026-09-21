using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Configuration;
using AgentSmith.Application.Services.Resume;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Configuration.Resolved;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services.Resume;

/// <summary>
/// 2026-09-22-7c41a: a run sized to a complexity tier parks on a question and comes back.
/// The raised cap cannot cross the checkpoint and the scope step that raised it never
/// re-runs, so the relaunch recomputes it from the carried tier — the configuration as it
/// stands NOW, raised to that tier, which keeps the raise a merge in both directions.
/// </summary>
public sealed class ResumedCostCapTests
{
    private const string Pipeline = "phase-execution";
    private readonly RecordingEventPublisher _events = EventTestStubs.Recording();

    [Fact]
    public async Task Resume_ARunSizedToATierCap_ComesBackUnderThatCapNotTheConfiguredOne()
    {
        var pipeline = Parked(ComplexityTier.Large);

        await Recompute(configured: new CostCapValues { Usd = 5m, Tokens = 500_000 })
            .ApplyAsync(pipeline, Pipeline, TierTable(), CancellationToken.None);

        Cap(pipeline).Usd.Should().Be(30m);
        Cap(pipeline).Tokens.Should().Be(15_000_000);
    }

    [Fact]
    public async Task Resume_ARunWithNoRaisedTier_ComesBackUnderTheConfiguredCap()
    {
        var pipeline = Parked(tier: null);

        await Recompute(configured: new CostCapValues { Usd = 5m, Tokens = 500_000 })
            .ApplyAsync(pipeline, Pipeline, TierTable(), CancellationToken.None);

        Cap(pipeline).Usd.Should().Be(5m, "the launch seed is left exactly as it was");
        Cap(pipeline).Tokens.Should().Be(500_000);
        _events.Events.Should().BeEmpty("nothing about this run's budget changed");
    }

    // A checkpoint written before this phase carries no tier at all — the same shape as a
    // run whose tier was Unknown, and the accepted limitation for runs parked before it.
    [Fact]
    public async Task Checkpoint_ACheckpointWrittenBeforeThisPhase_ResumesOnTheConfiguredCap()
    {
        var pipeline = Parked(ComplexityTier.Unknown);

        await Recompute(configured: new CostCapValues { Usd = 5m, Tokens = 500_000 })
            .ApplyAsync(pipeline, Pipeline, TierTable(), CancellationToken.None);

        Cap(pipeline).Usd.Should().Be(5m);
        Cap(pipeline).Tokens.Should().Be(500_000);
    }

    [Fact]
    public async Task Resume_TheOperatorRaisedTheCapWhileParked_TheRaiseIsNotLost()
    {
        var pipeline = Parked(ComplexityTier.Large);

        // The operator raised the per-pipeline entry past the tier's own ceiling mid-park.
        await Recompute(configured: new CostCapValues { Usd = 80m, Tokens = 40_000_000 })
            .ApplyAsync(pipeline, Pipeline, TierTable(), CancellationToken.None);

        Cap(pipeline).Usd.Should().Be(80m);
        Cap(pipeline).Tokens.Should().Be(40_000_000);
    }

    [Fact]
    public async Task Resume_TheOperatorLoweredTheCapWhileParked_TheTierRaiseStillApplies()
    {
        var pipeline = Parked(ComplexityTier.Large);

        await Recompute(configured: new CostCapValues { Usd = 1m, Tokens = 50_000 })
            .ApplyAsync(pipeline, Pipeline, TierTable(), CancellationToken.None);

        Cap(pipeline).Usd.Should().Be(30m, "an estimate raises the operator's number, never shrinks it");
        Cap(pipeline).Tokens.Should().Be(15_000_000);
        // The recomputed cap may legitimately differ from the one the row recorded, so the
        // run row is re-stated rather than left contradicting what the run now runs against.
        _events.Events.OfType<RunBudgetResolvedEvent>().Should().ContainSingle()
            .Which.CapUsd.Should().Be(30m);
    }

    private static PipelineContext Parked(ComplexityTier? tier)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "run-7c41a");
        // What every launch seeds, a resume included.
        pipeline.Set("PipelineCostCap", new CostCapValues { Usd = 5m, Tokens = 500_000 });
        if (tier is not null) pipeline.Set(ContextKeys.ComplexityTier, tier.Value);
        return pipeline;
    }

    private static CostCapValues Cap(PipelineContext pipeline) =>
        pipeline.TryGet<CostCapValues>("PipelineCostCap", out var cap) && cap is not null
            ? cap
            : throw new InvalidOperationException("the run carries no cost cap");

    private static AgentSmithConfig TierTable() => new()
    {
        PipelineCostCap = new PipelineCostCapConfig
        {
            Default = new CostCapValues { Usd = 5m, Tokens = 500_000 },
            PerTier = new Dictionary<ComplexityTier, CostCapValues>
            {
                [ComplexityTier.Large] = new() { Usd = 30m, Tokens = 15_000_000 },
            },
        },
    };

    private ResumedCapRecompute Recompute(CostCapValues configured) =>
        new(new FixedCapResolver(configured), _events, NullLogger<ResumedCapRecompute>.Instance);

    private sealed class FixedCapResolver(CostCapValues cap) : IConfigResolver
    {
        public ResolvedValue<CostCapValues> ResolveCostCap(string? pipelineName) =>
            ResolvedValue<CostCapValues>.Global(cap);

        public ResolvedProjectSettings Resolve(ResolvedProject project) =>
            throw new NotSupportedException();

        public ResolvedValue<int> ResolveStepTimeout(ResolvedProject project) =>
            throw new NotSupportedException();

        public ResolvedValue<int> ResolveRunCommandTimeout(ResolvedProject project) =>
            throw new NotSupportedException();

        public ResolvedConfig Materialize() => throw new NotSupportedException();
    }
}
