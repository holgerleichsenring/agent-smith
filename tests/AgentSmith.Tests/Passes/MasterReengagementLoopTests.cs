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
        var rig = new LoopRig(SpecPipeline(), ProgressStatus.Pending, runContext, ScriptedPasses.Text("still red", toolCalls: 0));

        await rig.RunAsync(Red);

        var (first, second) = (rig.Events.Ends[0], rig.Events.Ends[1]);
        (first.Ordinal, first.PassInStep, first.OpenedBecause, first.EndedBecause, first.ReengageReason).Should().Be(
            (1, 0, MasterPassReasons.First, MasterPassReasons.Reengaged, MasterPassReasons.RedWithOpenSteps));
        (second.Ordinal, second.PassInStep, second.OpenedBecause, second.ReengageReason).Should().Be(
            (2, 1, MasterPassReasons.RedWithOpenSteps, (string?)null));
    }

    /// <summary>2026-11-18-98fa: a pass's verdict is its own finish_spec call — one that made none emitted none, and
    /// the gate line names the standing finish_spec status rather than counting done lines.</summary>
    [Fact]
    public async Task PassRecorder_PassWithoutFinishSpec_EmitsNone_AndTheGateLineNamesTheStanding()
    {
        var runContext = new AsyncLocalRunContextAccessor();
        using var step = runContext.BeginStepScope(3, Spec);
        var rig = new LoopRig(SpecPipeline("it builds"), ProgressStatus.Pending, runContext,
            ScriptedPasses.Text("still working", toolCalls: 0));

        await rig.RunAsync(Red);

        var (first, silent) = (rig.Events.Ends[0], rig.Events.Ends[1]);
        (first.VerdictEmitted, first.CriteriaMet, first.Criteria).Should().Be((true, (int?)null, (IReadOnlyList<MasterPassCriterion>?)null));
        silent.VerdictEmitted.Should().BeFalse();
        silent.GateLine.Should().Be("finish_spec: Failed");
    }

    /// <summary>2026-11-18-98fa: a re-engaged pass that calls finish_spec emits its verdict with the report.</summary>
    [Fact]
    public async Task ReengagedPass_CallingFinishSpec_EmitsItsVerdictWithTheReport()
    {
        var runContext = new AsyncLocalRunContextAccessor();
        using var step = runContext.BeginStepScope(3, Spec);
        var rig = new LoopRig(SpecPipeline("it builds"), ProgressStatus.Done, runContext, Finish("green"));

        await rig.RunAsync(null);

        rig.Events.Ends.Should().HaveCount(2);
        (rig.Events.Ends[0].ReengageReason, rig.Events.Ends[1].VerdictEmitted, rig.Events.Ends[1].EndedBecause)
            .Should().Be((MasterPassReasons.Unfinished, true, MasterPassReasons.NothingLeft));
        rig.Finish.Verification!.Status.Should().Be(VerificationStatus.Green);
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
            MasterPassReasons.Idle => ScriptedPasses.Text("still red", toolCalls: 0),
            MasterPassReasons.Blocked => Finish("blocked", "no access"),
            MasterPassReasons.Budget => ScriptedPasses.Throws(new MasterBudgetExhaustedException("budget")),
            MasterPassReasons.Question => () =>
            {
                rig!.Clarifications.AskHuman("Which branch?");
                return Finish("failed")();
            },
            _ => Finish("failed"),
        };
        rig = new LoopRig(pipeline, drained ? ProgressStatus.Done : ProgressStatus.Pending, runContext, pass);
        return rig;
    }
}
