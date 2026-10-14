using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Pipeline;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>2026-10-13-8762g: the attempt boundary — SpecSequence splices only the specs before a stop, defers
/// the rest to the next attempt with the reason, and a parked stop is handed back with SpecHandback after.</summary>
public sealed class SpecSequenceStopTests
{
    private const string Reason = "review has used its 40% share of the run's wall clock (500 s of 1000 s)";

    [Fact]
    public async Task SpecSequence_Stop_SplicesOnlyTheSpecsBeforeIt_TheRestDeferredWithTheReason()
    {
        var pipeline = Pipeline(new SpecSequenceStop("p0001b", Reason, Park: false));

        var result = await Sequence(pipeline);

        result.InsertNext!.Select(c => c.SpecId).Distinct().Should().Equal("p0001a");
        result.InsertNext!.Should().HaveCount(PipelinePresets.CodeSpecBlock.Count);
        pipeline.Get<StepBudget>(ContextKeys.StepBudget).Should().Be(StepBudget.ForSpecs(1));
        var progress = pipeline.Get<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress).Specs;
        progress.Select(p => (p.SpecId, p.State, p.NextAttempt, p.Note)).Should().Equal(
            ("p0001a", SpecRunState.NotStarted, false, null),
            ("p0001b", SpecRunState.NotStarted, true, Reason),
            ("p0001c", SpecRunState.NotStarted, true, Reason));
        result.Message.Should().Contain($"stopping before p0001b: {Reason}");
    }

    [Fact]
    public async Task SpecSequence_DeferredSpec_PrTableNamesTheReason()
    {
        var pipeline = Pipeline(new SpecSequenceStop("p0001c", Reason, Park: false));

        await Sequence(pipeline);
        var table = SpecStatusTable.Render(pipeline.Get<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress));

        table.Should().Contain($"| p0001c | Goal of p0001c | ⬜ not started — {Reason} |");
        table.Should().Contain("| p0001a | Goal of p0001a | ⬜ not started |");
    }

    [Fact]
    public void SequenceProgress_Deferring_SeedsNotStartedWithNextAttempt()
    {
        var progress = new SpecSequenceProgress([
            new SpecProgress("a", "A", SpecRunState.Done),
            new SpecProgress("b", "B", SpecRunState.InProgress, FailingCommand: "kept")]);

        var deferred = progress.Deferring(["b"], Reason).Specs;

        deferred[0].Should().Be(progress.Specs[0]);
        deferred[1].Should().Be(new SpecProgress("b", "B", SpecRunState.NotStarted, "kept", Reason, NextAttempt: true));
    }

    [Fact]
    public async Task SpecSequence_StopWithPark_SeedsHandedBackAndAppendsSpecHandback()
    {
        var pipeline = Pipeline(new SpecSequenceStop("p0001b", "the review's objection stands", Park: true));

        var result = await Sequence(pipeline);

        result.InsertNext!.Select(c => c.Name).Should().Equal(
            [.. PipelinePresets.CodeSpecBlock, CommandNames.SpecHandback]);
        var progress = pipeline.Get<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress).Specs;
        progress[1].Should().Match<SpecProgress>(p => p.State == SpecRunState.HandedBack
            && p.FailingCommand == "the review's objection stands" && !p.NextAttempt);
        progress[2].NextAttempt.Should().BeTrue();
    }

    private static PipelineContext Pipeline(SpecSequenceStop stop)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecSet, new SpecSet(
            "azdo-1",
            [.. new[] { "p0001a", "p0001b", "p0001c" }.Select(id => new DerivedSpec(
                new SpecDraft(id, $"Goal of {id}", $"spec: {id}", []), id, string.Empty, []))],
            SpecAccounting.Empty, [new SpecRevision(1, "initial derivation", DateTimeOffset.UtcNow)], SpecSource.Derived));
        pipeline.Set(ContextKeys.SpecSequenceStop, stop);
        return pipeline;
    }

    private static Task<CommandResult> Sequence(PipelineContext pipeline) =>
        new SpecSequenceHandler(AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Reader(), TimeProvider.System, NullLogger<SpecSequenceHandler>.Instance)
            .ExecuteAsync(new SpecSequenceContext(pipeline), CancellationToken.None);
}
