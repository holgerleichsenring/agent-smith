using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Progress;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Handlers;

// p0341c: the open loop re-engages while budget + actionable ledger steps remain and each
// pass makes MEANINGFUL forward progress — bounded by MONEY + progress, never a fixed count.
// These pin the two pure predicates the driver stands on.
public sealed class AgenticMasterReengageTests
{
    private static ProgressLedger Ledger(params ProgressStatus[] statuses)
    {
        var entries = new ProgressLedgerEntry[statuses.Length];
        for (var i = 0; i < statuses.Length; i++)
            entries[i] = new ProgressLedgerEntry((i + 1).ToString(), $"step {i + 1}", statuses[i]);
        return new ProgressLedger(entries);
    }

    private static MasterVerification Verdict(VerificationStatus status) =>
        new(status, true, status != VerificationStatus.Failed, true,
            status is VerificationStatus.Green, "summary");

    [Fact]
    public void ShouldReengage_PartialLedgerActionableStepsAndBudgetRemain_True()
    {
        AgenticMasterHandler.ShouldReengage(
            "fix-bug", Ledger(ProgressStatus.Done, ProgressStatus.Pending),
            Verdict(VerificationStatus.Green), budgetExhausted: false)
            .Should().BeTrue();
    }

    [Fact]
    public void ShouldReengage_BudgetExhausted_False()
    {
        AgenticMasterHandler.ShouldReengage(
            "fix-bug", Ledger(ProgressStatus.Pending),
            Verdict(VerificationStatus.Green), budgetExhausted: true)
            .Should().BeFalse();
    }

    [Fact]
    public void ShouldReengage_FailedVerdict_False_NotReDrivenIntoLoop()
    {
        // An honest RED is respected — the loop does not grind a failed run.
        AgenticMasterHandler.ShouldReengage(
            "fix-bug", Ledger(ProgressStatus.Pending),
            Verdict(VerificationStatus.Failed), budgetExhausted: false)
            .Should().BeFalse();
    }

    [Fact]
    public void ShouldReengage_LedgerDrained_False()
    {
        AgenticMasterHandler.ShouldReengage(
            "fix-bug", Ledger(ProgressStatus.Done, ProgressStatus.Done),
            Verdict(VerificationStatus.Green), budgetExhausted: false)
            .Should().BeFalse();
    }

    [Fact]
    public void ShouldReengage_NonCodePipeline_False()
    {
        AgenticMasterHandler.ShouldReengage(
            "security-scan", Ledger(ProgressStatus.Pending),
            Verdict(VerificationStatus.Green), budgetExhausted: false)
            .Should().BeFalse();
    }

    [Fact]
    public void Reengage_NewlyDoneStep_IsForwardProgress()
    {
        AgenticMasterHandler.MadeForwardProgress(
            doneStepsBefore: 1, doneStepsAfter: 2,
            verificationBefore: Verdict(VerificationStatus.Failed),
            verificationAfter: Verdict(VerificationStatus.Failed),
            passEndedOnException: false)
            .Should().BeTrue();
    }

    [Fact]
    public void Reengage_BareEditNoVerifiedSignal_NotCountedAsMeaningfulProgress()
    {
        // Same done-count, same non-passing verdict — a bare edit is NOT progress.
        AgenticMasterHandler.MadeForwardProgress(
            doneStepsBefore: 1, doneStepsAfter: 1,
            verificationBefore: Verdict(VerificationStatus.Failed),
            verificationAfter: Verdict(VerificationStatus.Failed),
            passEndedOnException: false)
            .Should().BeFalse();
    }

    [Fact]
    public void Reengage_ZeroForwardProgressPass_Stops()
    {
        AgenticMasterHandler.MadeForwardProgress(
            doneStepsBefore: 2, doneStepsAfter: 2,
            verificationBefore: Verdict(VerificationStatus.Green),
            verificationAfter: Verdict(VerificationStatus.Green),
            passEndedOnException: false)
            .Should().BeFalse();
    }

    [Fact]
    public void Reengage_VerdictNowPasses_IsForwardProgress()
    {
        AgenticMasterHandler.MadeForwardProgress(
            doneStepsBefore: 1, doneStepsAfter: 1,
            verificationBefore: Verdict(VerificationStatus.Failed),
            verificationAfter: Verdict(VerificationStatus.Green),
            passEndedOnException: false)
            .Should().BeTrue();
    }

    [Fact]
    public void Reengage_PassEndedOnException_NotCountedAsZeroProgress()
    {
        AgenticMasterHandler.MadeForwardProgress(
            doneStepsBefore: 2, doneStepsAfter: 2,
            verificationBefore: Verdict(VerificationStatus.Green),
            verificationAfter: Verdict(VerificationStatus.Green),
            passEndedOnException: true)
            .Should().BeTrue();
    }

    [Fact]
    public void Reengage_NudgeCarriesWorkingStateBlock_NotJustLedger()
    {
        var decisions = new[] { new PlanDecision("Architecture", "handler signature is (cmd, ct)") };
        var block = AgenticMasterHandler.BuildWorkingStateBlock(decisions, Verdict(VerificationStatus.Green));

        block.Should().Contain("Working state");
        block.Should().Contain("handler signature is (cmd, ct)");
        block.Should().Contain("Last build/test");
    }
}
