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

    // A Green verdict that ALSO reports every ratified criterion Met — an objectively
    // satisfied acceptance contract.
    private static MasterVerification GreenWithMet(int criteriaCount) =>
        new(VerificationStatus.Green, true, true, true, true, "summary",
            AcceptanceDispositions: Enumerable.Range(0, criteriaCount)
                .Select(i => new AcceptanceDisposition($"criterion {i}", AcceptanceStatus.Met, "edit X"))
                .ToList());

    private static readonly IReadOnlyList<string> NoCriteria = System.Array.Empty<string>();
    private static readonly IReadOnlyList<CodeChange> NoChanges = System.Array.Empty<CodeChange>();

    [Fact]
    public void ShouldReengage_PartialLedgerActionableStepsAndBudgetRemain_True()
    {
        AgenticMasterHandler.ShouldReengage(
            "fix-bug", Ledger(ProgressStatus.Done, ProgressStatus.Pending),
            Verdict(VerificationStatus.Green), budgetExhausted: false, NoCriteria, NoChanges)
            .Should().BeTrue();
    }

    [Fact]
    public void ShouldReengage_BudgetExhausted_False()
    {
        AgenticMasterHandler.ShouldReengage(
            "fix-bug", Ledger(ProgressStatus.Pending),
            Verdict(VerificationStatus.Green), budgetExhausted: true, NoCriteria, NoChanges)
            .Should().BeFalse();
    }

    [Fact]
    public void ShouldReengage_RedWithActionablePending_Reengages()
    {
        // p0363: RED with open actionable items is a status report mid-work, not a
        // verdict of impossibility — the observed gpt-5.1 failure: verification red,
        // ledger NOW item literally "fix the build", $43 budget left, model stops.
        // The forward-progress gate still ends the loop after one unproductive red
        // pass, so persistence stays bounded.
        AgenticMasterHandler.ShouldReengage(
            "fix-bug", Ledger(ProgressStatus.Pending),
            Verdict(VerificationStatus.Failed), budgetExhausted: false, NoCriteria, NoChanges)
            .Should().BeTrue();
    }

    [Fact]
    public void ShouldReengage_RedWithDrainedLedger_Stops()
    {
        // p0363: honest RED with nothing actionable left IS the verdict — justified
        // surrender stays respected.
        AgenticMasterHandler.ShouldReengage(
            "fix-bug", Ledger(ProgressStatus.Done, ProgressStatus.Done),
            Verdict(VerificationStatus.Failed), budgetExhausted: false, NoCriteria, NoChanges)
            .Should().BeFalse();
    }

    [Fact]
    public void ShouldReengage_LedgerDrained_NoContract_False()
    {
        AgenticMasterHandler.ShouldReengage(
            "fix-bug", Ledger(ProgressStatus.Done, ProgressStatus.Done),
            Verdict(VerificationStatus.Green), budgetExhausted: false, NoCriteria, NoChanges)
            .Should().BeFalse();
    }

    [Fact]
    public void ShouldReengage_NonCodePipeline_False()
    {
        AgenticMasterHandler.ShouldReengage(
            "security-scan", Ledger(ProgressStatus.Pending),
            Verdict(VerificationStatus.Green), budgetExhausted: false, NoCriteria, NoChanges)
            .Should().BeFalse();
    }

    [Fact]
    public void ShouldReengage_ModelMarkedAllDoneButAcceptanceObjectivelyUnmet_StillReengages()
    {
        // The model drained the ledger (all steps done) and reported Green — but the ratified
        // acceptance contract has an UNMET criterion. Objective incompleteness wins over the
        // model's self-reported "all done": re-engage rather than let the loop quit early.
        var criteria = new[] { "Server updated", "BackgroundWorker updated" };
        var verdict = new MasterVerification(
            VerificationStatus.Green, true, true, true, true, "summary",
            AcceptanceDispositions: new[]
            {
                new AcceptanceDisposition("Server updated", AcceptanceStatus.Met, "edited Server.cs"),
                new AcceptanceDisposition("BackgroundWorker updated", AcceptanceStatus.Unmet, ""),
            });

        AgenticMasterHandler.ShouldReengage(
            "fix-bug", Ledger(ProgressStatus.Done, ProgressStatus.Done),
            verdict, budgetExhausted: false, criteria, NoChanges)
            .Should().BeTrue();
    }

    [Fact]
    public void ShouldReengage_ModelMarkedDoneButTargetAbsentFromDiff_StillReengages()
    {
        // The ledger is drained and the step is marked done, but its declared target never
        // appears in the diff — marking-without-doing. The unfakeable diff drives re-engagement.
        var ledger = new ProgressLedger(new[]
        {
            new ProgressLedgerEntry("1", "add worker", ProgressStatus.Done, Target: "src/Worker.cs"),
        });
        var changes = new[]
        {
            new CodeChange(new FilePath("src/Unrelated.cs"), "x", "modified"),
        };

        AgenticMasterHandler.ShouldReengage(
            "fix-bug", ledger, Verdict(VerificationStatus.Green),
            budgetExhausted: false, NoCriteria, changes)
            .Should().BeTrue();
    }

    [Fact]
    public void ShouldReengage_GenuinelyDone_ContractMetAndDiffBacks_Stops()
    {
        // Drained ledger, every ratified criterion reported Met, the done step's target IS in
        // the diff — genuinely complete. The loop stops.
        var ledger = new ProgressLedger(new[]
        {
            new ProgressLedgerEntry("1", "update server", ProgressStatus.Done, Target: "src/Server.cs"),
        });
        var changes = new[]
        {
            new CodeChange(new FilePath("src/Server.cs"), "x", "modified"),
        };

        AgenticMasterHandler.ShouldReengage(
            "fix-bug", ledger, GreenWithMet(1),
            budgetExhausted: false, new[] { "Server updated" }, changes)
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
