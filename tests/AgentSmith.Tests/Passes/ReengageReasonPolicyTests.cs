using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Passes;
using AgentSmith.Contracts.Progress;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Passes;

/// <summary>
/// 2026-10-13-9a59f: the reason the open loop gives is the branch it took, and the stops a red, blocked or silent
/// master reaches are named for what they are. 2026-11-18-98fa: nothing is judged per done line — a master that
/// called finish_spec green with nothing open is done.
/// </summary>
public sealed class ReengageReasonPolicyTests
{
    private static readonly IReadOnlyList<CodeChange> Changed = [new CodeChange(new FilePath("src/A.cs"), "x", "modified")];

    private static ProgressLedger Ledger(ProgressStatus status, string? target = null) =>
        new([new ProgressLedgerEntry("s1", "do it", status, Target: target)]);

    private static MasterVerification Verdict(VerificationStatus status) => new(status, true, true, true, true, "s");

    private static ReengageDecision Decide(
        ProgressLedger ledger, MasterVerification? verdict, IReadOnlyList<CodeChange> changes, int pass = 1) =>
        ReengageReasonPolicy.Decide(PipelinePresets.CodeName, ledger, verdict, budgetExhausted: false, changes,
            pass, verdictDemandedInPass: false);

    [Fact]
    public void Reengage_RedWithOpenSteps_NamesThatReason() =>
        Decide(Ledger(ProgressStatus.Pending), Verdict(VerificationStatus.Failed), [])
            .Should().Be(new ReengageDecision(true, MasterPassReasons.RedWithOpenSteps));

    [Fact]
    public void Reengage_UnbackedDoneSteps_NamesThatReason() =>
        Decide(Ledger(ProgressStatus.Done, "src/Worker.cs"), Verdict(VerificationStatus.Green), Changed)
            .Should().Be(new ReengageDecision(true, MasterPassReasons.UnbackedDone));

    [Fact]
    public void Reengage_GreenFinish_IsNothingLeft_WhateverTheDoneLines() =>
        Decide(Ledger(ProgressStatus.Done), Verdict(VerificationStatus.Green), Changed)
            .Should().Be(new ReengageDecision(false, MasterPassReasons.NothingLeft));

    [Fact]
    public void Reengage_ABlockerFromFinishSpec_StopsBlocked_EvenWithOpenSteps() =>
        Decide(Ledger(ProgressStatus.Pending), Verdict(VerificationStatus.Failed) with { Blocker = "the feed answers 401" }, [])
            .Should().Be(new ReengageDecision(false, MasterPassReasons.Blocked));

    [Fact]
    public void Reengage_RedDrainedLedger_StopsRedAccepted() =>
        Decide(Ledger(ProgressStatus.Done), Verdict(VerificationStatus.Failed), Changed)
            .Should().Be(new ReengageDecision(false, MasterPassReasons.RedAccepted));

    [Fact]
    public void Reengage_NoFinishSpec_DrivesOnce_ThenStopsVerdictless()
    {
        Decide(Ledger(ProgressStatus.Done), null, Changed, pass: 1)
            .Should().Be(new ReengageDecision(true, MasterPassReasons.Unfinished), "a master without finish_spec gets one re-drive");
        Decide(Ledger(ProgressStatus.Done), null, Changed, pass: 2)
            .Should().Be(new ReengageDecision(false, MasterPassReasons.Verdictless));
    }

    [Fact]
    public void Reengage_Budget_IsANamedStop() =>
        ReengageReasonPolicy.Decide(PipelinePresets.CodeName, Ledger(ProgressStatus.Pending), Verdict(VerificationStatus.Failed),
            budgetExhausted: true, [], 1, false).Should().Be(new ReengageDecision(false, MasterPassReasons.Budget));
}
