using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Progress;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Handlers;

// p0365: the trajectory suite — the spec of "handles ALLES". Each archetype is a sequence
// of per-pass outcomes fed through the pure re-engagement policy; the assertions pin that a
// productive-but-incomplete run (big migration, greenfield UI ramp, moving repair) drives on,
// while genuine churn/idle stalls and an honest concrete blocker is respected. Was RED against
// the pre-change predicate (which stopped after one no-completed-step pass).
public sealed class ReengageProgressPolicyTests
{
    private const int K = ReengageProgressPolicy.DefaultPatience;

    private static MasterVerification Red(string summary) =>
        new(VerificationStatus.Failed, true, false, true, false, summary);

    private static MasterVerification Green() =>
        new(VerificationStatus.Green, true, true, true, true, "green");

    // ---- Classify: state change is progress, identical red error is churn ----

    [Fact]
    public void Classify_NewEditsNoCompletedStep_Advanced() =>
        ReengageProgressPolicy.Classify(1, 1, Red("e1"), Red("e2"), changesBefore: 3, changesAfter: 5, 0, 0)
            .Should().Be(PassProgress.Advanced);

    [Fact]
    public void Classify_NewDecisionNoCompletedStep_Advanced() =>
        ReengageProgressPolicy.Classify(1, 1, null, null, 4, 4, decisionsBefore: 2, decisionsAfter: 3)
            .Should().Be(PassProgress.Advanced);

    [Fact]
    public void Classify_RepairEditsRedErrorMoved_Advanced() =>
        // Red→red but the build tail changed (errors moved) with new edits — a productive repair.
        ReengageProgressPolicy.Classify(1, 1, Red("12 errors"), Red("7 errors"), 3, 6, 0, 0)
            .Should().Be(PassProgress.Advanced);

    [Fact]
    public void Classify_EditsButSameRedError_StalledOnSameError() =>
        // Edited, yet the build is red with the IDENTICAL error — churning.
        ReengageProgressPolicy.Classify(1, 1, Red("same error"), Red("same error"), 3, 5, 0, 0)
            .Should().Be(PassProgress.StalledOnSameError);

    [Fact]
    public void Classify_NoStateChange_Stalled() =>
        ReengageProgressPolicy.Classify(1, 1, Red("x"), Red("x"), 5, 5, 2, 2)
            .Should().Be(PassProgress.Stalled);

    [Fact]
    public void Classify_StepCompleted_Completed() =>
        ReengageProgressPolicy.Classify(1, 2, Red("x"), Red("x"), 5, 5, 0, 0)
            .Should().Be(PassProgress.Completed);

    [Fact]
    public void Classify_VerdictNowGreen_Completed() =>
        ReengageProgressPolicy.Classify(1, 1, Red("x"), Green(), 5, 5, 0, 0)
            .Should().Be(PassProgress.Completed);

    // ---- ShouldRespectBlock: "can't" needs a concrete blocker, like "done" needs a diff ----

    [Fact]
    public void ShouldRespectBlock_ConcreteBlocker_True() =>
        ReengageProgressPolicy.ShouldRespectBlock(new MasterBlockedClaim(true, "broker connection string absent from config"))
            .Should().BeTrue();

    [Fact]
    public void ShouldRespectBlock_EmptyBlocker_False() =>
        ReengageProgressPolicy.ShouldRespectBlock(new MasterBlockedClaim(true, "  ")).Should().BeFalse();

    // ---- Trajectories: archetype = a sequence of pass outcomes ----

    private static (int passes, ReengageOutcome stoppedOn) RunTrajectory(
        params (PassProgress progress, MasterBlockedClaim? block, bool threw)[] passes)
    {
        var streak = 0;
        for (var i = 0; i < passes.Length; i++)
        {
            var (progress, block, threw) = passes[i];
            var step = ReengageProgressPolicy.Decide(progress, block, streak, K, threw);
            streak = step.Streak;
            if (step.Outcome != ReengageOutcome.Continue) return (i + 1, step.Outcome);
        }
        return (passes.Length, ReengageOutcome.Continue);
    }

    private static (PassProgress, MasterBlockedClaim?, bool) Pass(PassProgress p) => (p, null, false);

    [Fact]
    public void Reengage_BigStepAcrossProductivePasses_DrivesToCompletion()
    {
        // A large step: several productive passes with no completed step, then completions.
        // Advanced never charges patience, so the loop drives through instead of aborting.
        var result = RunTrajectory(
            Pass(PassProgress.Advanced), Pass(PassProgress.Advanced), Pass(PassProgress.Advanced),
            Pass(PassProgress.Advanced), Pass(PassProgress.Completed), Pass(PassProgress.Completed));
        result.stoppedOn.Should().Be(ReengageOutcome.Continue);
        result.passes.Should().Be(6);
    }

    [Fact]
    public void Reengage_GreenfieldNoStepRamp_DoesNotAbort()
    {
        // Greenfield UI: a long ramp of scaffolding/components, no completed step, no green
        // build yet — every pass moves state. It must not abort.
        var result = RunTrajectory(Enumerable.Range(0, 8).Select(_ => Pass(PassProgress.Advanced)).ToArray());
        result.stoppedOn.Should().Be(ReengageOutcome.Continue);
        result.passes.Should().Be(8);
    }

    [Fact]
    public void Reengage_RepetitionStallSameErrorKTimes_EscalatesToHuman()
    {
        // Editing every pass but the build stays red on the identical error K times — churn.
        var result = RunTrajectory(Enumerable.Range(0, K).Select(_ => Pass(PassProgress.StalledOnSameError)).ToArray());
        result.stoppedOn.Should().Be(ReengageOutcome.StopStalled);
        result.passes.Should().Be(K);
    }

    [Fact]
    public void PatienceCounter_ResetByAProductivePass_DoesNotAccumulateAcrossCompletions()
    {
        // Same-error churn interleaved with a productive pass never reaches the patience bound.
        var result = RunTrajectory(
            Pass(PassProgress.StalledOnSameError), Pass(PassProgress.StalledOnSameError),
            Pass(PassProgress.Advanced),   // resets the streak
            Pass(PassProgress.StalledOnSameError), Pass(PassProgress.StalledOnSameError));
        result.stoppedOn.Should().Be(ReengageOutcome.Continue);
    }

    [Fact]
    public void Reengage_IdlePass_StopsImmediately()
    {
        RunTrajectory(Pass(PassProgress.Stalled)).stoppedOn.Should().Be(ReengageOutcome.StopStalled);
    }

    [Fact]
    public void Reengage_HonestBlockedConcreteBlocker_StopsAndRecords()
    {
        var block = new MasterBlockedClaim(true, "step 4 needs a broker connection string only the operator has");
        var result = RunTrajectory((PassProgress.Advanced, block, false));
        result.stoppedOn.Should().Be(ReengageOutcome.StopBlocked);
    }

    [Fact]
    public void Reengage_FakeImpossibleNoBlocker_ReDrivesWithNudge()
    {
        // "too complex" with no concrete blocker is the can't-side of faking-green — re-driven.
        var vague = new MasterBlockedClaim(true, null);
        var result = RunTrajectory((PassProgress.Advanced, vague, false), (PassProgress.Completed, null, false));
        result.stoppedOn.Should().Be(ReengageOutcome.Continue);
    }

    [Fact]
    public void Reengage_PassEndedOnException_ContinuesAsRecovery()
    {
        ReengageProgressPolicy.Decide(PassProgress.Stalled, null, streak: 0, K, passEndedOnException: true)
            .Outcome.Should().Be(ReengageOutcome.Continue);
    }

    // ---- Lazy-done stays a ShouldReengage concern: a done step whose target is absent from the diff ----

    [Fact]
    public void Reengage_LazyDoneWithoutDiff_KeepsDriving()
    {
        var ledger = new ProgressLedger(new[]
        {
            new ProgressLedgerEntry("1", "update server", ProgressStatus.Done, Target: "src/Server.cs"),
        });
        var noBackingDiff = System.Array.Empty<CodeChange>();
        AgenticMasterHandler.ShouldReengage(
            "fix-bug", ledger, Green(), budgetExhausted: false, new[] { "Server updated" }, noBackingDiff)
            .Should().BeTrue();
    }

    // ---- Blocked-claim parser: ready to consume a { blocked, blocker } block ----

    [Fact]
    public void TryParseBlockedClaim_ConcreteBlocker_Parsed()
    {
        var claim = MasterVerificationParser.TryParseBlockedClaim(
            "Here is my status.\n```json\n{\"blocked\": true, \"blocker\": \"missing NuGet feed credentials\"}\n```");
        claim.Should().NotBeNull();
        claim!.IsBlocked.Should().BeTrue();
        claim.Blocker.Should().Be("missing NuGet feed credentials");
    }

    [Fact]
    public void TryParseBlockedClaim_Absent_Null() =>
        MasterVerificationParser.TryParseBlockedClaim("All good, no blockers here.").Should().BeNull();
}
