using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0439: a run that stopped after a verified phase, with a phase still open, is a
/// shortfall — a done that says what it lacks. Reachable from exactly that shape.
/// </summary>
public sealed class RunShortfallTests
{
    private static readonly SpecSequenceProgress TwoPhases = new(
    [
        new PhaseProgress("p0001a", "Introduce the guard", PhaseRunState.NotStarted),
        new PhaseProgress("p0001b", "Move the callers", PhaseRunState.NotStarted),
    ]);

    [Fact]
    public void Of_AVerifiedPhaseAndAnOpenOne_SplitsThemAndCarriesTheReason()
    {
        var progress = TwoPhases.With("p0001a", PhaseRunState.Done).With("p0001b", PhaseRunState.InProgress);

        var shortfall = RunShortfall.Of(progress, " per-pipeline cost budget exhausted ");

        shortfall.Should().NotBeNull();
        shortfall!.Delivered.Select(p => p.PhaseId).Should().Equal("p0001a");
        shortfall.NotDelivered.Select(p => p.PhaseId).Should().Equal("p0001b");
        shortfall.Reason.Should().Be("per-pipeline cost budget exhausted");
        shortfall.PhaseCount.Should().Be(2);
        shortfall.Summary.Should().Contain("Delivered 1 of 2 phase(s)").And.Contain("p0001b").And.Contain("cost budget");
    }

    [Fact]
    public void Of_ARunWhoseFirstPhaseFailed_IsNotAShortfall()
    {
        var progress = TwoPhases.With("p0001a", PhaseRunState.Failed, "dotnet build exited 1");

        RunShortfall.Of(progress, "dotnet build exited 1").Should().BeNull(
            "nothing verified means nothing to deliver — the run stays failed");
    }

    [Fact]
    public void Of_ARunWhoseEveryPhaseIsDone_IsNotAShortfall()
    {
        var progress = TwoPhases.With("p0001a", PhaseRunState.Done).With("p0001b", PhaseRunState.Done);

        RunShortfall.Of(progress, "PR open failed").Should().BeNull(
            "the contract was met; whatever failed afterwards is not a shortfall of it");
    }

    [Fact]
    public void Of_NoFailureAndNoProgress_IsNull()
    {
        var progress = TwoPhases.With("p0001a", PhaseRunState.Done);

        RunShortfall.Of(progress, null).Should().BeNull();
        RunShortfall.Of(progress, "  ").Should().BeNull();
        RunShortfall.Of(null, "budget").Should().BeNull();
    }

    [Fact]
    public void Of_ThePipeline_ReadsTheFailureAndTheTable()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecSequenceProgress, TwoPhases.With("p0001a", PhaseRunState.Done));
        pipeline.Set(ContextKeys.FailureReason, "budget");

        RunShortfall.Of(pipeline).Should().NotBeNull();
        RunShortfall.Of(new PipelineContext()).Should().BeNull();
    }

    [Fact]
    public void DeliveredOn_OnlyAfterMarkDelivered()
    {
        var pipeline = new PipelineContext();
        var shortfall = RunShortfall.Of(TwoPhases.With("p0001a", PhaseRunState.Done), "budget")!;

        RunShortfall.DeliveredOn(pipeline).Should().BeNull("a shortfall that was not delivered is a failed run");
        shortfall.MarkDelivered(pipeline);

        RunShortfall.DeliveredOn(pipeline).Should().BeSameAs(shortfall);
    }
}
