using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Passes;
using AgentSmith.Contracts.Progress;
using AgentSmith.Contracts.Providers;
using FluentAssertions;
using static AgentSmith.Tests.Passes.MasterPassFixtures;

namespace AgentSmith.Tests.Passes;

/// <summary>
/// 2026-10-13-9a59f: the open loop records every pass of a spec — the pass it was handed ends with the
/// loop's first decision, each re-engagement pass opens with the reason it was re-engaged for, and every
/// exit ends exactly the open pass, once, with its own word. 2026-11-22-96c8a: the loop carries the standing report.
/// </summary>
public sealed class MasterReengagementLoopTests
{
    private const string FirstReport = "## Delivered\n- the widget store, built and tested";

    public static TheoryData<string> Exits() => new()
    {
        MasterPassReasons.NothingLeft, MasterPassReasons.StepsOpen, MasterPassReasons.Idle,
        MasterPassReasons.Question, MasterPassReasons.Budget,
    };

    [Theory]
    [MemberData(nameof(Exits))]
    public async Task PassRecorder_EveryLoopExit_PublishesOneEndWithItsReason(string exit)
    {
        var runContext = new AsyncLocalRunContextAccessor();
        using var step = runContext.BeginStepScope(3, Spec);
        var rig = Rig(exit, runContext);

        await rig.RunAsync(FirstReport);

        var ends = rig.Events.Ends;
        ends.Select(e => e.Ordinal).Should().OnlyHaveUniqueItems("each pass ends once");
        ends.Should().HaveCount(exit == MasterPassReasons.NothingLeft ? 1 : 2);
        ends[^1].EndedBecause.Should().Be(exit);
        ends.SkipLast(1).Should().OnlyContain(e => e.EndedBecause == MasterPassReasons.Reengaged);
        ends.Should().OnlyContain(e => e.SpecId == Spec);
    }

    [Fact]
    public async Task PassRecorder_ReengagedPass_CarriesTheReengageReason()
    {
        var runContext = new AsyncLocalRunContextAccessor();
        using var step = runContext.BeginStepScope(3, Spec);
        var rig = new LoopRig(SpecPipeline(), ProgressStatus.Pending, runContext, ScriptedPasses.Text("still open", toolCalls: 0));

        await rig.RunAsync(FirstReport);

        var (first, second) = (rig.Events.Ends[0], rig.Events.Ends[1]);
        (first.Ordinal, first.PassInStep, first.OpenedBecause, first.EndedBecause, first.ReengageReason).Should().Be(
            (1, 0, MasterPassReasons.First, MasterPassReasons.Reengaged, MasterPassReasons.Unfinished));
        (second.Ordinal, second.PassInStep, second.OpenedBecause, second.ReengageReason).Should().Be(
            (2, 1, MasterPassReasons.Unfinished, (string?)null));
    }

    /// <summary>2026-11-22-96c8a: VerdictEmitted says the pass ended on text of its own; the gate line whether a
    /// report stands. A re-drive's ending text is added to the report, so pass 0's is never lost.</summary>
    [Fact]
    public async Task ReengagedPass_EndingOnText_AppendsToTheReport_AndTheGateLineSaysSo()
    {
        var runContext = new AsyncLocalRunContextAccessor();
        using var step = runContext.BeginStepScope(3, Spec);
        var rig = new LoopRig(SpecPipeline("it builds"), ProgressStatus.Pending, runContext, Report("## Delivered again\n- all of it"));

        await rig.RunAsync(FirstReport);

        var (first, second) = (rig.Events.Ends[0], rig.Events.Ends[1]);
        (first.VerdictEmitted, first.GateLine).Should().Be((true, "report written"));
        (second.VerdictEmitted, second.GateLine).Should().Be((true, "report written"));
        rig.Report.Should().Be(FirstReport + "\n\n## Delivered again\n- all of it");
    }

    /// <summary>2026-11-22-96c8a: a re-drive cut on a call (the iteration ceiling) or by the budget keeps the report
    /// the master wrote before.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReengagedPass_Cut_KeepsTheEarlierReport(bool budget)
    {
        var runContext = new AsyncLocalRunContextAccessor();
        using var step = runContext.BeginStepScope(3, Spec);
        var rig = new LoopRig(SpecPipeline("it builds"), ProgressStatus.Pending, runContext,
            budget ? ScriptedPasses.Throws(new MasterBudgetExhaustedException("budget")) : CutOnACall(2));

        await rig.RunAsync(FirstReport);

        rig.Report.Should().Be(FirstReport);
        rig.Events.Ends[1].VerdictEmitted.Should().BeFalse();
        rig.Events.Ends[1].GateLine.Should().Be("report written");
    }

    [Fact]
    public async Task NoReportAnywhere_TheGateLineSaysNoReport()
    {
        var runContext = new AsyncLocalRunContextAccessor();
        using var step = runContext.BeginStepScope(3, Spec);
        var rig = new LoopRig(SpecPipeline("it builds"), ProgressStatus.Pending, runContext, CutOnACall(1));

        await rig.RunAsync(null);

        rig.Report.Should().BeNull();
        rig.Events.Ends.Should().OnlyContain(e => e.GateLine == "no report" && !e.VerdictEmitted);
    }

    private static LoopRig Rig(string exit, AsyncLocalRunContextAccessor runContext)
    {
        var pipeline = SpecPipeline();
        LoopRig? rig = null;
        var pass = exit switch
        {
            MasterPassReasons.Idle => ScriptedPasses.Text("still open", toolCalls: 0),
            MasterPassReasons.Budget => ScriptedPasses.Throws(new MasterBudgetExhaustedException("budget")),
            MasterPassReasons.Question => () =>
            {
                rig!.Clarifications.AskHuman("Which branch?");
                return Report()();
            },
            _ => Report(),
        };
        rig = new LoopRig(pipeline, exit == MasterPassReasons.NothingLeft ? ProgressStatus.Done : ProgressStatus.Pending, runContext, pass);
        return rig;
    }
}
