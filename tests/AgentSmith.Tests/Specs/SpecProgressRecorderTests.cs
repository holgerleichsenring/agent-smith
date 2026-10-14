using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0466: one writer of a spec's standing. It goes into the run's per-spec table AND
/// onto the event stream, because the stream is the only DB channel a spawned
/// orchestrator has — a spec recorded in the pull request but not in the store is a
/// spec the operator still cannot open afterwards.
/// </summary>
public sealed class SpecProgressRecorderTests
{
    private const string RunId = "2026-08-19T09-00-00-0001";

    [Fact]
    public async Task Record_InProgress_UpdatesTheTableAndAnnouncesTheSpec()
    {
        var (pipeline, publisher, recorder) = Setup();

        await recorder.RecordAsync(pipeline, "p0001b", SpecRunState.InProgress);

        pipeline.Get<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress)
            .Specs.Single(p => p.SpecId == "p0001b").State
            .Should().Be(SpecRunState.InProgress);
        var announced = publisher.Events.OfType<SpecStateChangedEvent>().Single();
        announced.SpecId.Should().Be("p0001b");
        announced.Ordinal.Should().Be(2, "the ordinal is the spec's place in the sequence");
        announced.Title.Should().Be("Goal of p0001b");
        announced.State.Should().Be(SpecRunState.InProgress);
        announced.Verdict.Should().BeNull();
    }

    [Fact]
    public async Task Record_Failed_CarriesTheFailingCommandAsTheVerdict()
    {
        var (pipeline, publisher, recorder) = Setup();

        await recorder.RecordAsync(pipeline, "p0001a", SpecRunState.Failed, "dotnet test exited 1");

        publisher.Events.OfType<SpecStateChangedEvent>().Single().Verdict
            .Should().Be("dotnet test exited 1");
    }

    [Fact]
    public async Task Record_Note_BecomesTheVerdictWhenNoCommandFailed()
    {
        var (pipeline, publisher, recorder) = Setup();

        await recorder.RecordAsync(
            pipeline, "p0001a", SpecRunState.Done, note: "already satisfied by the branch");

        publisher.Events.OfType<SpecStateChangedEvent>().Single().Verdict
            .Should().Be("already satisfied by the branch");
    }

    /// <summary>
    /// A run with no spec set has no specs, so there is nothing to record and nothing to
    /// announce — never an empty spec row for an ordinary ticket.
    /// </summary>
    [Fact]
    public async Task Record_RunWithoutASpecSet_RecordsNothing()
    {
        var publisher = EventTestStubs.Recording();
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, RunId);

        await new SpecProgressRecorder(publisher)
            .RecordAsync(pipeline, "p0001a", SpecRunState.Done);

        publisher.Events.Should().BeEmpty();
        pipeline.TryGet<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress, out _)
            .Should().BeFalse();
    }

    /// <summary>2026-10-13-9a59e: a spec's verdict rides on its own state event — its accounts only.</summary>
    [Fact]
    public async Task SpecProgressRecorder_Done_CarriesThatSpecsCriteriaOnly()
    {
        var (pipeline, publisher, recorder) = Setup();
        pipeline.Set(ContextKeys.RunAccounts, AgentSmith.Domain.Models.RunAccounts.Empty
            .With("p0001a", [new("api", [new("p0001a is done.", AgentSmith.Domain.Models.AccountDisposition.Satisfied, "src/A.cs")])])
            .With("p0001b", [new("api", [new("p0001b is done.", AgentSmith.Domain.Models.AccountDisposition.NotSatisfied)])]));

        await recorder.RecordAsync(pipeline, "p0001a", SpecRunState.Done);

        var acceptance = AgentSmith.Contracts.Runs.RunStoryJson.TryDeserialize<AgentSmith.Contracts.Runs.AcceptanceView>(
            publisher.Events.OfType<SpecStateChangedEvent>().Single().AcceptanceJson)!;
        acceptance.Criteria.Select(c => (c.Text, c.Status)).Should().Equal(("p0001a is done.", "met"));
    }

    [Fact]
    public async Task SpecProgressRecorder_FailedOnRedBuild_CarriesTheUnmeasuredProblem()
    {
        var (pipeline, publisher, recorder) = Setup();
        pipeline.Set(ContextKeys.CurrentSpec, TwoSpecSet().Specs[1].Draft);
        Application.Services.Specs.RunAccountLedger.RecordProblem(pipeline, ["api"], "dotnet build exited 1");

        await recorder.RecordAsync(pipeline, "p0001b", SpecRunState.Failed, "dotnet build exited 1");
        await recorder.RecordAsync(pipeline, "p0001a", SpecRunState.InProgress);

        var events = publisher.Events.OfType<SpecStateChangedEvent>().ToList();
        AgentSmith.Contracts.Runs.RunStoryJson.TryDeserialize<AgentSmith.Contracts.Runs.AcceptanceView>(events[0].AcceptanceJson)!
            .Unmeasured.Single().Reason.Should().Be("dotnet build exited 1");
        events[1].AcceptanceJson.Should().BeNull("a spec with no account yet carries none");
    }

    private static (PipelineContext, RecordingEventPublisher, SpecProgressRecorder) Setup()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, RunId);
        pipeline.Set(ContextKeys.SpecSet, TwoSpecSet());
        var publisher = EventTestStubs.Recording();
        return (pipeline, publisher, new SpecProgressRecorder(publisher));
    }

    private static SpecSet TwoSpecSet() => new(
        "azdo-1",
        [.. new[] { "p0001a", "p0001b" }.Select(id => new DerivedSpec(
            new SpecDraft(id, $"Goal of {id}", $"spec: {id}", []) { Done = [$"{id} is done."] },
            id, string.Empty, []))],
        SpecAccounting.Empty,
        [new SpecRevision(1, "initial derivation", DateTimeOffset.UtcNow)],
        SpecSource.Derived);
}
