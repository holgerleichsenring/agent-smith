using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Passes;
using AgentSmith.Contracts.Progress;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Passes;

/// <summary>
/// 2026-10-13-9a59f: the reason the open loop gives is the branch ShouldReengage took — three re-drives,
/// and the stops a red or silent master reaches are named for what they are, never "nothing left".
/// </summary>
public sealed class ReengageReasonPolicyTests
{
    private static readonly IReadOnlyList<string> Criteria = ["it builds"];
    private static readonly IReadOnlyList<CodeChange> Changed = [new CodeChange(new FilePath("src/A.cs"), "x", "modified")];

    private static ProgressLedger Ledger(ProgressStatus status, string? target = null) =>
        new([new ProgressLedgerEntry("s1", "do it", status, Target: target)]);

    private static MasterVerification Verdict(VerificationStatus status) => new(status, true, true, true, true, "s");

    private static ReengageDecision Decide(
        ProgressLedger ledger, MasterVerification? verdict, IReadOnlyList<string> criteria,
        IReadOnlyList<CodeChange> changes, int pass = 1) =>
        ReengageReasonPolicy.Decide(PipelinePresets.CodeName, ledger, verdict, budgetExhausted: false, criteria, changes,
            pass, verdictDemandedInPass: false);

    [Fact]
    public void Reengage_RedWithOpenSteps_NamesThatReason() =>
        Decide(Ledger(ProgressStatus.Pending), Verdict(VerificationStatus.Failed), [], [])
            .Should().Be(new ReengageDecision(true, MasterPassReasons.RedWithOpenSteps));

    [Fact]
    public void Reengage_UnbackedDoneSteps_NamesThatReason() =>
        Decide(Ledger(ProgressStatus.Done, "src/Worker.cs"), Verdict(VerificationStatus.Green), [], Changed)
            .Should().Be(new ReengageDecision(true, MasterPassReasons.UnbackedDone));

    [Fact]
    public void Reengage_GateUnsatisfied_NamesThatReason() =>
        Decide(Ledger(ProgressStatus.Done), Verdict(VerificationStatus.Green), Criteria, Changed)
            .Should().Be(new ReengageDecision(true, MasterPassReasons.GateUnsatisfied));

    [Fact]
    public void Reengage_RedDrainedLedger_StopsRedAccepted() =>
        Decide(Ledger(ProgressStatus.Done), Verdict(VerificationStatus.Failed), Criteria, Changed)
            .Should().Be(new ReengageDecision(false, MasterPassReasons.RedAccepted));

    [Fact]
    public void Reengage_VerdictlessAfterRedrive_StopsVerdictless()
    {
        Decide(Ledger(ProgressStatus.Done), null, Criteria, Changed, pass: 1)
            .Should().Be(new ReengageDecision(true, MasterPassReasons.GateUnsatisfied), "a silent master gets one re-drive");
        Decide(Ledger(ProgressStatus.Done), null, Criteria, Changed, pass: 2)
            .Should().Be(new ReengageDecision(false, MasterPassReasons.Verdictless));
    }

    [Fact]
    public void Reengage_BudgetAndNothingLeft_AreNamedStops()
    {
        ReengageReasonPolicy.Decide(PipelinePresets.CodeName, Ledger(ProgressStatus.Pending), Verdict(VerificationStatus.Failed),
            budgetExhausted: true, [], [], 1, false).Should().Be(new ReengageDecision(false, MasterPassReasons.Budget));
        Decide(Ledger(ProgressStatus.Done), Verdict(VerificationStatus.Green), [], [])
            .Should().Be(new ReengageDecision(false, MasterPassReasons.NothingLeft));
    }
}
