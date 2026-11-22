using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Passes;
using AgentSmith.Contracts.Progress;
using FluentAssertions;

namespace AgentSmith.Tests.Passes;

/// <summary>
/// 2026-10-13-9a59f: the reason the open loop gives is the branch it took. 2026-11-22-96c8a: no verdict exists — open
/// actionable steps get one re-drive, a drained ledger stops whether or not a report was written. The two spend shifts
/// against the finish_spec era are pinned here.
/// </summary>
public sealed class ReengageReasonPolicyTests
{
    private static ProgressLedger Ledger(ProgressStatus status, string? target = null) =>
        new([new ProgressLedgerEntry("s1", "do it", status, Target: target)]);

    private static ReengageDecision Decide(ProgressLedger ledger, int pass = 1) =>
        ReengageReasonPolicy.Decide(PipelinePresets.CodeName, ledger, budgetExhausted: false, pass);

    [Fact]
    public void Reengage_OpenSteps_DrivesOnce_ThenStopsStepsOpen()
    {
        Decide(Ledger(ProgressStatus.Pending), pass: 1)
            .Should().Be(new ReengageDecision(true, MasterPassReasons.Unfinished), "open steps get one re-drive");
        Decide(Ledger(ProgressStatus.Pending), pass: 2)
            .Should().Be(new ReengageDecision(false, MasterPassReasons.StepsOpen));
    }

    [Fact]
    public void Reengage_SpendShift_OpenStepsReDriveOnce_WhereAGreenFinishOnceStopped() =>
        Decide(Ledger(ProgressStatus.InProgress))
            .Should().Be(new ReengageDecision(true, MasterPassReasons.Unfinished),
                "a master that would have called finish_spec green over open steps stopped nothing-left; it is now re-driven once");

    [Fact]
    public void Reengage_SpendShift_TextOnlyEndOverADrainedLedger_GetsNoReDrive() =>
        Decide(Ledger(ProgressStatus.Done))
            .Should().Be(new ReengageDecision(false, MasterPassReasons.NothingLeft),
                "a text-only end over a drained ledger once got the verdict nudge and a re-drive; it now gets none");

    [Fact]
    public void Reengage_DoneStepsTheDiffDoesNotBack_StopNothingLeft() =>
        Decide(Ledger(ProgressStatus.Done, "src/Worker.cs"))
            .Should().Be(new ReengageDecision(false, MasterPassReasons.NothingLeft));

    [Fact]
    public void Reengage_Budget_IsANamedStop() =>
        ReengageReasonPolicy.Decide(PipelinePresets.CodeName, Ledger(ProgressStatus.Pending), budgetExhausted: true, 1)
            .Should().Be(new ReengageDecision(false, MasterPassReasons.Budget));

    [Fact]
    public void Reengage_OffACodePipeline_StopsWithoutAReason() =>
        ReengageReasonPolicy.Decide("security-scan", Ledger(ProgressStatus.Pending), budgetExhausted: false, 1)
            .Should().Be(new ReengageDecision(false, null));
}
