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
/// p0393a: the sequence is where TERMINATION comes from — each spec ends at its own
/// done-list and its own VerifySpec, so stopping is structural instead of a judgement
/// the model has to reach.
/// </summary>
public sealed class SpecSequenceTests
{
    [Fact]
    public async Task SpecSequence_TwoSpecs_SplicesOneBlockPerSpecInOrder()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecSet, TwoSpecSet());

        var result = await Sequence().ExecuteAsync(new SpecSequenceContext(pipeline), default);

        result.IsSuccess.Should().BeTrue();
        var spliced = result.InsertNext!;
        spliced.Should().HaveCount(PipelinePresets.CodeSpecBlock.Count * 2);
        spliced.Select(c => c.SpecId).Distinct().Should().Equal("p0001a", "p0001b");
        spliced.Take(PipelinePresets.CodeSpecBlock.Count).Select(c => c.Name)
            .Should().Equal(PipelinePresets.CodeSpecBlock);
    }

    [Fact]
    public async Task SpecSequence_ExecutedSpecs_AreNotRunAgain()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecSet, TwoSpecSet() with { Executed = ["p0001a"] });

        var result = await Sequence().ExecuteAsync(new SpecSequenceContext(pipeline), default);

        result.InsertNext!.Select(c => c.SpecId).Distinct().Should().Equal("p0001b");
    }

    [Fact]
    public async Task SelectSpec_MakesTheSpecCurrent()
    {
        // p0394a: publishing the draft is the whole handover — the master's plan
        // section and the ledger seed both read ContextKeys.CurrentSpec.
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecSet, TwoSpecSet());

        var result = await new SelectSpecHandler(
                NoEntryAccount(), StartHeads(), new SpecProgressRecorder(new NoOpEventPublisher()),
                NullLogger<SelectSpecHandler>.Instance)
            .ExecuteAsync(new SelectSpecContext("p0001b", pipeline), default);

        result.IsSuccess.Should().BeTrue();
        pipeline.Get<PhaseDraft>(ContextKeys.CurrentSpec).PhaseId.Should().Be("p0001b");
        pipeline.Get<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress)
            .Phases.Single(p => p.PhaseId == "p0001b").State
            .Should().Be(PhaseRunState.InProgress);
    }

    /// <summary>
    /// p0469: entering a spec ends the previous spec's evidence, the way p0444 ends its
    /// repair state. The account judges ONE spec's criteria, and a command that proved
    /// something about the spec before it is not evidence about this one.
    /// </summary>
    [Fact]
    public async Task SpecCommands_NewSpec_StartsWithoutThePreviousSpecsCommands()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecSet, TwoSpecSet());
        Application.Services.Specs.SpecRunCommandScope.Open(pipeline)
            .Record("api", "grep -rn 'Sample' src", "exit_code: 0\n");

        await new SelectSpecHandler(
                NoEntryAccount(), StartHeads(), new SpecProgressRecorder(new NoOpEventPublisher()),
                NullLogger<SelectSpecHandler>.Instance)
            .ExecuteAsync(new SelectSpecContext("p0001b", pipeline), default);

        Application.Services.Specs.SpecEvidence.From([], pipeline).Should().BeEmpty(
            "phase b is accounted for by what phase b ran");
    }

    [Fact]
    public void SpecSequence_StoppedMidway_ProgressReportsDoneFailedAndNotStarted()
    {
        var progress = SpecSequenceProgress.ForSet(ThreeSpecSet())
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
    public void SpecSequence_StoppedMidwayButDelivered_TableIsADeliveryNote_NotAWarning()
    {
        // p0439: the branch carries exactly the done specs, so the reader is pointed at
        // "Not delivered" instead of told not to merge.
        var progress = SpecSequenceProgress.ForSet(ThreeSpecSet())
            .With("p0001a", PhaseRunState.Done)
            .With("p0001b", PhaseRunState.Failed, "dotnet build exited 1");

        var table = Application.Services.Specs.SpecPrBody.RenderStatus(progress, delivered: true);

        table.Should().NotContain("DO NOT MERGE");
        table.Should().Contain("## Spec status").And.Contain("Not delivered");
        table.Should().Contain("✅ done").And.Contain("dotnet build exited 1");
    }

    [Fact]
    public void SpecSequence_SpecHandedBackOnAFalsePremise_ReadsUnlikeARedBuildInTheTable()
    {
        // 2026-09-17-0e79c: this switch's default arm is "⬜ not started", so a standing it does
        // not name is rendered as a spec nobody entered.
        var progress = SpecSequenceProgress.ForSet(ThreeSpecSet())
            .With("p0001a", PhaseRunState.Failed, "dotnet build exited 1")
            .With("p0001b", PhaseRunState.HandedBack, "False premise in p0001b: \"…\" — [M1] …");

        var table = Application.Services.Specs.SpecPrBody.RenderStatus(progress);

        table.Should().Contain("handed back, never built")
            .And.Contain("False premise in p0001b")
            .And.NotContain("❌ failed — False premise", "a false premise is not a red build");
    }

    [Fact]
    public void SpecSequence_EverySpecDone_IsNotPartial()
    {
        var progress = SpecSequenceProgress.ForSet(TwoSpecSet())
            .With("p0001a", PhaseRunState.Done)
            .With("p0001b", PhaseRunState.Done);

        progress.IsPartial.Should().BeFalse(
            "only a complete sequence may take its pull request out of draft");
    }

    private static SpecSequenceHandler Sequence() =>
        new(NullLogger<SpecSequenceHandler>.Instance);

    // p0460: no sandboxes in this pipeline, so the entry account resolves nothing and the
    // spec is entered the way it always was. SpecEntryAccountTests owns the account.
    private static Application.Services.Specs.SpecEntryAccount NoEntryAccount() =>
        new(AgentSmith.Tests.TestHelpers.TestGit.Delivery,
            new Application.Services.Specs.SpecDeliveryAccounting(
                AgentSmith.Tests.TestHelpers.TestGit.Delivery,
                null!, new SandboxTargets(),
                NullLogger<Application.Services.Specs.SpecDeliveryAccounting>.Instance),
            new SandboxTargets(),
            NullLogger<Application.Services.Specs.SpecEntryAccount>.Instance);

    private static SpecSet TwoSpecSet() => Set(["p0001a", "p0001b"]);

    private static SpecSet ThreeSpecSet() => Set(["p0001a", "p0001b", "p0001c"]);

    private static SpecSet Set(IReadOnlyList<string> specIds) => new(
        "azdo-1",
        [.. specIds.Select(id => new SpecPhase(
            new PhaseDraft(id, $"Goal of {id}", $"spec: {id}", []) { Done = [$"{id} is done."] },
            id, string.Empty, []))],
        SpecAccounting.Empty,
        [new SpecRevision(1, "initial derivation", DateTimeOffset.UtcNow)],
        SpecSource.Derived);

    /// <summary>2026-09-17-042eh: a start-head recorder with no sandbox to resolve — it
    /// records an empty map, which is what a test pipeline without sandboxes has.</summary>
    private static Application.Services.Specs.SpecStartHeads StartHeads() =>
        AgentSmith.Tests.TestHelpers.TestSpecReview.StartHeads();
}
