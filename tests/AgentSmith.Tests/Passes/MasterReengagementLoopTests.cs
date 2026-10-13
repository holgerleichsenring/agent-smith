using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Passes;
using AgentSmith.Contracts.Progress;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using FluentAssertions;
using static AgentSmith.Tests.Passes.MasterPassFixtures;

namespace AgentSmith.Tests.Passes;

/// <summary>
/// 2026-10-13-9a59f: the open loop records every pass of a spec — the pass it was handed ends with the
/// loop's first decision, each re-engagement pass opens with the reason it was re-engaged for, and every
/// exit ends exactly the open pass, once, with its own word.
/// </summary>
public sealed class MasterReengagementLoopTests
{
    private static readonly MasterVerification Red = new(VerificationStatus.Failed, true, false, true, false, "red");
    private static readonly MasterVerification Green = new(VerificationStatus.Green, true, true, true, true, "green");

    public static TheoryData<string> Exits() => new()
    {
        MasterPassReasons.NothingLeft, MasterPassReasons.RedAccepted, MasterPassReasons.Idle, MasterPassReasons.Blocked,
        MasterPassReasons.Question, MasterPassReasons.Budget, MasterPassReasons.Cap,
    };

    [Theory]
    [MemberData(nameof(Exits))]
    public async Task PassRecorder_EveryLoopExit_PublishesOneEndWithItsReason(string exit)
    {
        var runContext = new AsyncLocalRunContextAccessor();
        using var step = runContext.BeginStepScope(3, Spec);
        var rig = Rig(exit, runContext);

        await rig.RunAsync(exit is MasterPassReasons.NothingLeft ? Green : Red);

        var ends = rig.Events.Ends;
        ends.Select(e => e.Ordinal).Should().OnlyHaveUniqueItems("each pass ends once");
        ends.Should().HaveCount(Expected(exit));
        ends[^1].EndedBecause.Should().Be(exit);
        ends.SkipLast(1).Should().OnlyContain(e => e.EndedBecause == MasterPassReasons.Reengaged);
        ends.Should().OnlyContain(e => e.SpecId == Spec);
    }

    [Fact]
    public async Task PassRecorder_ReengagedPass_CarriesTheReengageReason()
    {
        var runContext = new AsyncLocalRunContextAccessor();
        using var step = runContext.BeginStepScope(3, Spec);
        var rig = new LoopRig(SpecPipeline(), ProgressStatus.Pending, runContext, ScriptedPasses.Text(Verdict("red"), toolCalls: 0));

        await rig.RunAsync(Red);

        var (first, second) = (rig.Events.Ends[0], rig.Events.Ends[1]);
        (first.Ordinal, first.PassInStep, first.OpenedBecause, first.EndedBecause, first.ReengageReason).Should().Be(
            (1, 0, MasterPassReasons.First, MasterPassReasons.Reengaged, MasterPassReasons.RedWithOpenSteps));
        (second.Ordinal, second.PassInStep, second.OpenedBecause, second.ReengageReason).Should().Be(
            (2, 1, MasterPassReasons.RedWithOpenSteps, (string?)null));
    }

    [Fact]
    public async Task PassRecorder_PassWithoutVerdict_HasNullCriteriaCounts()
    {
        var runContext = new AsyncLocalRunContextAccessor();
        using var step = runContext.BeginStepScope(3, Spec);
        var rig = new LoopRig(SpecPipeline("it builds", "it is documented"), ProgressStatus.Pending, runContext,
            ScriptedPasses.Text("still working, no verdict yet"), ScriptedPasses.Text("nothing", toolCalls: 0));
        var counted = Red with
        {
            AcceptanceDispositions = [new AcceptanceDisposition("it builds", AcceptanceStatus.Met, "dotnet build"),
                new AcceptanceDisposition("it is documented", AcceptanceStatus.Unmet, "")],
        };

        await rig.RunAsync(counted);

        var (first, silent) = (rig.Events.Ends[0], rig.Events.Ends[1]);
        (first.VerdictEmitted, first.CriteriaMet, first.CriteriaTotal).Should().Be((true, (int?)null, (int?)null),
            "a red verdict over changed nothing is a verdict-level refusal — no counts");
        first.Criteria!.Select(c => (c.Criterion, c.Status)).Should().Equal(("it builds", "met"), ("it is documented", "unmet"));
        (silent.VerdictEmitted, silent.CriteriaMet, silent.CriteriaTotal, silent.Criteria)
            .Should().Be((false, (int?)null, (int?)null, (IReadOnlyList<MasterPassCriterion>?)null));
        silent.GateLine.Should().Contain("cannot stand", "the gate still judges the verdict the loop stands on");
    }

    [Fact]
    public async Task PassRecorder_GreenPassWithDispositions_CountsItsOwnCriteria()
    {
        var runContext = new AsyncLocalRunContextAccessor();
        using var step = runContext.BeginStepScope(3, Spec);
        var rig = new LoopRig(SpecPipeline("it builds", "it is documented"), ProgressStatus.Done, runContext,
            ScriptedPasses.Text("unused"));
        var green = Green with
        {
            AcceptanceDispositions = [new AcceptanceDisposition("it builds", AcceptanceStatus.Met, "dotnet build"),
                new AcceptanceDisposition("it is documented", AcceptanceStatus.Met, "README.md")],
        };

        await rig.RunAsync(green);

        var end = rig.Events.Ends.Should().ContainSingle().Subject;
        (end.CriteriaMet, end.CriteriaTotal, end.EndedBecause).Should().Be(((int?)2, (int?)2, MasterPassReasons.NothingLeft));
        end.GateLine.Should().StartWith("2 of 2 criteria satisfied");
        rig.Runner.Calls.Should().Be(0, "nothing was left to re-engage for");
    }

    private static int Expected(string exit) => exit switch
    {
        MasterPassReasons.NothingLeft or MasterPassReasons.RedAccepted => 1,
        MasterPassReasons.Cap => 1 + 50,
        _ => 2,
    };

    private static LoopRig Rig(string exit, AsyncLocalRunContextAccessor runContext)
    {
        var pipeline = SpecPipeline();
        var drained = exit is MasterPassReasons.NothingLeft or MasterPassReasons.RedAccepted;
        LoopRig? rig = null;
        var pass = exit switch
        {
            MasterPassReasons.Idle => ScriptedPasses.Text(Verdict("red"), toolCalls: 0),
            MasterPassReasons.Blocked => ScriptedPasses.Text(Verdict("red", extra: "\"blocked\": true, \"blocker\": \"no access\"")),
            MasterPassReasons.Budget => ScriptedPasses.Throws(new MasterBudgetExhaustedException("budget")),
            MasterPassReasons.Question => () =>
            {
                rig!.Clarifications.AskHuman("Which branch?");
                return ScriptedPasses.Text(Verdict("red"))();
            },
            _ => ScriptedPasses.Text(Verdict("red")),
        };
        rig = new LoopRig(pipeline, drained ? ProgressStatus.Done : ProgressStatus.Pending, runContext, pass);
        return rig;
    }
}
