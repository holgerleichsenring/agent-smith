using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0393a: the sequence is where TERMINATION comes from — each phase ends at its own
/// done-list and its own VerifyPhase, so stopping is structural instead of a judgement
/// the model has to reach.
/// </summary>
public sealed class PhaseSequenceTests
{
    [Fact]
    public async Task PhaseSequence_TwoPhases_SplicesOneBlockPerPhaseInOrder()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecSet, TwoPhaseSet());

        var result = await Sequence().ExecuteAsync(new PhaseSequenceContext(pipeline), default);

        result.IsSuccess.Should().BeTrue();
        var spliced = result.InsertNext!;
        spliced.Should().HaveCount(PipelinePresets.CodePhaseBlock.Count * 2);
        spliced.Select(c => c.PhaseId).Distinct().Should().Equal("p0001a", "p0001b");
        spliced.Take(PipelinePresets.CodePhaseBlock.Count).Select(c => c.Name)
            .Should().Equal(PipelinePresets.CodePhaseBlock);
    }

    [Fact]
    public async Task PhaseSequence_ExecutedPhases_AreNotRunAgain()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecSet, TwoPhaseSet() with { Executed = ["p0001a"] });

        var result = await Sequence().ExecuteAsync(new PhaseSequenceContext(pipeline), default);

        result.InsertNext!.Select(c => c.PhaseId).Distinct().Should().Equal("p0001b");
    }

    [Fact]
    public async Task SelectPhase_MakesThePhaseCurrent()
    {
        // p0394a: publishing the draft is the whole handover — the master's plan
        // section and the ledger seed both read ContextKeys.PhaseSpec.
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecSet, TwoPhaseSet());

        var result = await new SelectPhaseHandler(
                NoEntryAccount(), new PhaseProgressRecorder(new NoOpEventPublisher()),
                NullLogger<SelectPhaseHandler>.Instance)
            .ExecuteAsync(new SelectPhaseContext("p0001b", pipeline), default);

        result.IsSuccess.Should().BeTrue();
        pipeline.Get<PhaseDraft>(ContextKeys.PhaseSpec).PhaseId.Should().Be("p0001b");
        pipeline.Get<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress)
            .Phases.Single(p => p.PhaseId == "p0001b").State
            .Should().Be(PhaseRunState.InProgress);
    }

    /// <summary>
    /// p0469: entering a phase ends the previous phase's evidence, the way p0444 ends its
    /// repair state. The account judges ONE phase's criteria, and a command that proved
    /// something about the phase before it is not evidence about this one.
    /// </summary>
    [Fact]
    public async Task PhaseCommands_NewPhase_StartsWithoutThePreviousPhasesCommands()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecSet, TwoPhaseSet());
        Application.Services.Specs.PhaseCommandScope.Open(pipeline)
            .Record("api", "grep -rn 'Sample' src", "exit_code: 0\n");

        await new SelectPhaseHandler(
                NoEntryAccount(), new PhaseProgressRecorder(new NoOpEventPublisher()),
                NullLogger<SelectPhaseHandler>.Instance)
            .ExecuteAsync(new SelectPhaseContext("p0001b", pipeline), default);

        Application.Services.Specs.PhaseEvidence.From([], pipeline).Should().BeEmpty(
            "phase b is accounted for by what phase b ran");
    }

    [Fact]
    public void PhaseSequence_StoppedMidway_ProgressReportsDoneFailedAndNotStarted()
    {
        var progress = SpecSequenceProgress.ForSet(ThreePhaseSet())
            .With("p0001a", PhaseRunState.Done)
            .With("p0001b", PhaseRunState.Failed, "dotnet build exited 1");

        progress.IsPartial.Should().BeTrue("a half-migrated repository is the dangerous state");
        var table = Application.Services.Specs.SpecPrBody.RenderStatus(progress);
        table.Should().Contain("DO NOT MERGE");
        table.Should().Contain("✅ done");
        table.Should().Contain("dotnet build exited 1", "the failing command is named, not implied");
        table.Should().Contain("⬜ not started");
    }

    [Fact]
    public void PhaseSequence_EveryPhaseDone_IsNotPartial()
    {
        var progress = SpecSequenceProgress.ForSet(TwoPhaseSet())
            .With("p0001a", PhaseRunState.Done)
            .With("p0001b", PhaseRunState.Done);

        progress.IsPartial.Should().BeFalse(
            "only a complete sequence may take its pull request out of draft");
    }

    private static PhaseSequenceHandler Sequence() =>
        new(NullLogger<PhaseSequenceHandler>.Instance);

    // p0460: no sandboxes in this pipeline, so the entry account resolves nothing and the
    // phase is entered the way it always was. PhaseEntryAccountTests owns the account.
    private static Application.Services.Specs.PhaseEntryAccount NoEntryAccount() =>
        new(new Application.Services.DeliveryDiff(
                AgentSmith.Tests.TestHelpers.TestGit.BaseBranch,
                NullLogger<Application.Services.DeliveryDiff>.Instance),
            new Application.Services.Specs.PhaseAccounting(
                new Application.Services.DeliveryDiff(
                    AgentSmith.Tests.TestHelpers.TestGit.BaseBranch,
                    NullLogger<Application.Services.DeliveryDiff>.Instance),
                null!, new SandboxTargets(),
                NullLogger<Application.Services.Specs.PhaseAccounting>.Instance),
            new SandboxTargets(),
            NullLogger<Application.Services.Specs.PhaseEntryAccount>.Instance);

    private static SpecSet TwoPhaseSet() => Set(["p0001a", "p0001b"]);

    private static SpecSet ThreePhaseSet() => Set(["p0001a", "p0001b", "p0001c"]);

    private static SpecSet Set(IReadOnlyList<string> phaseIds) => new(
        "azdo-1",
        [.. phaseIds.Select(id => new SpecPhase(
            new PhaseDraft(id, $"Goal of {id}", $"phase: {id}", []) { Done = [$"{id} is done."] },
            id, string.Empty, []))],
        SpecAccounting.Empty,
        [new SpecRevision(1, "initial derivation", DateTimeOffset.UtcNow)],
        SpecSource.Derived);
}
