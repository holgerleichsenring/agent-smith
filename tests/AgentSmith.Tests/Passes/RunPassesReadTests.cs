using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Passes;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Passes;
using FluentAssertions;

namespace AgentSmith.Tests.Passes;

/// <summary>
/// 2026-10-13-9a59f: the passes read over its rows — numbered within their spec, a pass that died with
/// its run read as ended unknown, and every call outside a pass priced by what it ran in, never guessed.
/// </summary>
public sealed class RunPassesReadTests
{
    private const string RunId = "run-1";
    private const string A = "2026-10-13-1234a";
    private const string B = "2026-10-13-1234b";

    [Fact]
    public void Passes_Read_SecondSpecPasses_NumberOneAndTwoInSpec()
    {
        var view = Read(passes: [Pass(A, 1), Pass(A, 2), Pass(B, 3), Pass(B, 4)]);

        var second = view.Specs.Single(s => s.SpecId == B).Passes;
        second.Select(p => (p.Ordinal, p.PassInSpec)).Should().Equal((3, 1), (4, 2));
        view.Specs.Select(s => s.SpecId).Should().Equal(A, B);
    }

    [Fact]
    public void Passes_Read_CallsWithoutEndRow_ReadAsEndedUnknown()
    {
        var view = Read(passes: [Pass(A, 1)], calls: [Call(0.5m, spec: A, pass: 1), Call(0.25m, spec: A, pass: 2)]);

        var died = view.Specs.Single().Passes.Single(p => p.Ordinal == 2);
        died.EndedBecause.Should().Be(MasterPassReasons.Unknown);
        (died.OpenedBecause, died.VerdictEmitted, died.CostUsd, died.Calls, died.PassInSpec)
            .Should().Be(((string?)null, (bool?)null, 0.25m, 1, 2));
    }

    [Fact]
    public void Passes_Read_UnstampedCalls_AreGroupedByTheirStepsCommand()
    {
        var view = Read(
            calls: [Call(1m, step: 1, seq: 11), Call(2m, step: 3, seq: 31), Call(4m, step: 4, seq: 41),
                Call(8m, step: 2, seq: 21), Call(16m, spec: A, step: 2, seq: 22)],
            steps: [Step(1, CommandNames.DeriveSpec, 10), Step(2, CommandNames.AgenticMaster, 20),
                Step(3, CommandNames.CommitAndPR + ":api", 30), Step(4, CommandNames.PrCrossLink, 40)]);

        (view.Derive, view.Pr, view.Other).Should().Be((new RunPassCostView(1m, 1), new RunPassCostView(6m, 2), new RunPassCostView(8m, 1)));
        view.Specs.Single().OutsidePasses.Should().Be(new RunPassCostView(16m, 1), "a spec's call outside its passes is the spec's");
    }

    [Fact]
    public void Passes_Read_StepIndexReusedAfterPark_PricesByTheStepThatWasRunning()
    {
        var view = Read(
            calls: [Call(1m, step: 5, seq: 12), Call(2m, step: 5, seq: 55)],
            steps: [Step(5, CommandNames.DeriveSpec, 10), Step(5, CommandNames.CommitAndPR, 50)]);

        view.Derive.Should().Be(new RunPassCostView(1m, 1));
        view.Pr.Should().Be(new RunPassCostView(2m, 1));
    }

    [Fact]
    public void Passes_Read_CallWithoutStep_IsNotAttributed()
    {
        var view = Read(calls: [Call(3m, seq: 5), Call(1m, step: 9, seq: 6)], steps: [Step(1, CommandNames.DeriveSpec, 1)]);

        view.NotAttributed.Should().Be(new RunPassCostView(4m, 2));
        (view.Derive, view.Pr, view.Other).Should().Be((RunPassCostView.None, RunPassCostView.None, RunPassCostView.None));
    }

    [Fact]
    public void Passes_Read_PassListsTheStepsItMovedAndItsCriterionVerdicts()
    {
        var row = Pass(A, 1);
        row.CriteriaJson = """[{"criterion":"it builds","status":"met","evidence":"dotnet build"}]""";
        var view = Read(passes: [row, Pass(A, 2)], transitions:
        [
            Moved(1, """[{"entryId":"s1","activity":"guard","from":"pending","to":"done","cause":"model_update","pass":0}]"""),
            Moved(2, """[{"entryId":"s2","activity":"docs","from":"pending","to":"done","cause":"model_update","pass":1}]"""),
        ]);

        var first = view.Specs.Single().Passes[0];
        first.StepsMoved.Select(m => (m.EntryId, m.To)).Should().Equal(("s1", "done"));
        first.Criteria.Should().Equal(new MasterPassCriterion("it builds", "met", "dotnet build"));
        (first.ReengageReason, first.CriteriaMet).Should().Be((MasterPassReasons.RedWithOpenSteps, 1));
    }

    private static RunPassesView Read(
        IReadOnlyList<RunPass>? passes = null, IReadOnlyList<RunPassCall>? calls = null,
        IReadOnlyList<RunPassStep>? steps = null, IReadOnlyList<RunLedgerTransition>? transitions = null) =>
        RunPassesReader.Compose(RunId, new RunPassRecords(passes ?? [], transitions ?? [], calls ?? [], steps ?? []));

    private static RunPass Pass(string spec, int ordinal) => new()
    {
        RunId = RunId, SpecId = spec, Ordinal = ordinal, OpenedBecause = MasterPassReasons.First,
        EndedBecause = MasterPassReasons.Reengaged, ReengageReason = MasterPassReasons.RedWithOpenSteps,
        VerdictEmitted = true, CriteriaMet = 1, CriteriaTotal = 2, GateLine = "1 of 2 criteria satisfied",
    };

    private static RunPassCall Call(decimal cost, string? spec = null, int? pass = null, int? step = null, long? seq = null) =>
        new(seq, step, spec, pass, cost);

    private static RunPassStep Step(int index, string command, long seq) => new(seq, index, command);

    private static RunLedgerTransition Moved(int pass, string json) =>
        new() { RunId = RunId, SpecId = A, MasterPass = pass, TransitionsJson = json };
}
