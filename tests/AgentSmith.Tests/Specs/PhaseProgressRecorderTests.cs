using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0466: one writer of a phase's standing. It goes into the run's per-phase table AND
/// onto the event stream, because the stream is the only DB channel a spawned
/// orchestrator has — a phase recorded in the pull request but not in the store is a
/// phase the operator still cannot open afterwards.
/// </summary>
public sealed class PhaseProgressRecorderTests
{
    private const string RunId = "2026-08-19T09-00-00-0001";

    [Fact]
    public async Task Record_InProgress_UpdatesTheTableAndAnnouncesThePhase()
    {
        var (pipeline, publisher, recorder) = Setup();

        await recorder.RecordAsync(pipeline, "p0001b", PhaseRunState.InProgress);

        pipeline.Get<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress)
            .Phases.Single(p => p.PhaseId == "p0001b").State
            .Should().Be(PhaseRunState.InProgress);
        var announced = publisher.Events.OfType<PhaseStateChangedEvent>().Single();
        announced.PhaseId.Should().Be("p0001b");
        announced.Ordinal.Should().Be(2, "the ordinal is the phase's place in the sequence");
        announced.Title.Should().Be("Goal of p0001b");
        announced.State.Should().Be(PhaseRunState.InProgress);
        announced.Verdict.Should().BeNull();
    }

    [Fact]
    public async Task Record_Failed_CarriesTheFailingCommandAsTheVerdict()
    {
        var (pipeline, publisher, recorder) = Setup();

        await recorder.RecordAsync(pipeline, "p0001a", PhaseRunState.Failed, "dotnet test exited 1");

        publisher.Events.OfType<PhaseStateChangedEvent>().Single().Verdict
            .Should().Be("dotnet test exited 1");
    }

    [Fact]
    public async Task Record_Note_BecomesTheVerdictWhenNoCommandFailed()
    {
        var (pipeline, publisher, recorder) = Setup();

        await recorder.RecordAsync(
            pipeline, "p0001a", PhaseRunState.Done, note: "already satisfied by the branch");

        publisher.Events.OfType<PhaseStateChangedEvent>().Single().Verdict
            .Should().Be("already satisfied by the branch");
    }

    /// <summary>
    /// A run with no spec set has no phases, so there is nothing to record and nothing to
    /// announce — never an empty phase row for an ordinary ticket.
    /// </summary>
    [Fact]
    public async Task Record_RunWithoutASpecSet_RecordsNothing()
    {
        var publisher = EventTestStubs.Recording();
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, RunId);

        await new PhaseProgressRecorder(publisher)
            .RecordAsync(pipeline, "p0001a", PhaseRunState.Done);

        publisher.Events.Should().BeEmpty();
        pipeline.TryGet<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress, out _)
            .Should().BeFalse();
    }

    private static (PipelineContext, RecordingEventPublisher, PhaseProgressRecorder) Setup()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, RunId);
        pipeline.Set(ContextKeys.SpecSet, TwoPhaseSet());
        var publisher = EventTestStubs.Recording();
        return (pipeline, publisher, new PhaseProgressRecorder(publisher));
    }

    private static SpecSet TwoPhaseSet() => new(
        "azdo-1",
        [.. new[] { "p0001a", "p0001b" }.Select(id => new SpecPhase(
            new PhaseDraft(id, $"Goal of {id}", $"phase: {id}", []) { Done = [$"{id} is done."] },
            id, string.Empty, []))],
        SpecAccounting.Empty,
        [new SpecRevision(1, "initial derivation", DateTimeOffset.UtcNow)],
        SpecSource.Derived);
}
