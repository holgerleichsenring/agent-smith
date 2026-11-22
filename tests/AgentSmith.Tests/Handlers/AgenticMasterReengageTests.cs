using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Progress;
using AgentSmith.Domain.Entities;
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

    // 2026-11-22-96c8a: the old predicate's shape over ReengageReasonPolicy — no verdict decides anything any more.
    private static bool ShouldReengage(string pipeline, ProgressLedger ledger, bool budgetExhausted, int reengagePass) =>
        ReengageReasonPolicy.Decide(pipeline, ledger, budgetExhausted, reengagePass).Reengage;

    [Fact]
    public void ShouldReengage_OpenLedgerItems_ReengagesOnce()
    {
        ShouldReengage("code", Ledger(ProgressStatus.Done, ProgressStatus.Pending), budgetExhausted: false, reengagePass: 1)
            .Should().BeTrue();
        ShouldReengage("code", Ledger(ProgressStatus.Done, ProgressStatus.Pending), budgetExhausted: false, reengagePass: 2)
            .Should().BeFalse("a second re-drive is the same prompt against the same answer");
    }

    [Fact]
    public void ShouldReengage_BudgetExhausted_False() =>
        ShouldReengage("code", Ledger(ProgressStatus.Pending), budgetExhausted: true, reengagePass: 1).Should().BeFalse();

    [Fact]
    public void ShouldReengage_LedgerDrained_False() =>
        ShouldReengage("code", Ledger(ProgressStatus.Done, ProgressStatus.Done), budgetExhausted: false, reengagePass: 1)
            .Should().BeFalse();

    [Fact]
    public void ShouldReengage_NonCodePipeline_False() =>
        ShouldReengage("security-scan", Ledger(ProgressStatus.Pending), budgetExhausted: false, reengagePass: 1)
            .Should().BeFalse();

    // p0365: forward-progress classification moved from AgenticMasterHandler.MadeForwardProgress
    // to ReengageProgressPolicy (state-change + patience). Its unit + trajectory tests live in
    // ReengageProgressPolicyTests. The ShouldReengage tests above stay — that gate is unchanged.

    [Fact]
    public void Reengage_NudgeCarriesWorkingStateBlock_NotJustLedger()
    {
        var decisions = new[] { new PlanDecision("Architecture", "handler signature is (cmd, ct)") };
        var block = WorkingStateSection.Build(decisions);

        block.Should().Contain("Working state");
        block.Should().Contain("handler signature is (cmd, ct)");
        block.Should().NotContain("Last build/test", "2026-11-22-96c8a: no verdict tail any more");
    }

    // p0411: the state block answers "what have I changed?" so the pass does not
    // open by re-running git status / git diff.
    [Fact]
    public void WorkingState_CarriesTheChangedFileSummary()
    {
        var block = WorkingStateSection.Build(
            [], ["server/Api.cs", "server/Api.Tests.cs"]);

        block.Should().Contain("Changed files in the working tree (2)");
        block.Should().Contain("server/Api.cs");
        block.Should().Contain("server/Api.Tests.cs");
    }

    [Fact]
    public void WorkingState_ManyChangedFiles_ReportsTheCountAndTruncates()
    {
        // The block is re-sent every pass — it stays a summary, never an unbounded list.
        var paths = Enumerable.Range(0, 40).Select(i => $"src/File{i:00}.cs").ToList();

        var block = WorkingStateSection.Build([], paths);

        block.Should().Contain("Changed files in the working tree (40)");
        block.Should().Contain("+10 more");
        block.Should().NotContain("src/File39.cs");
    }

    [Fact]
    public void WorkingState_NotInspected_OmitsTheChangedFileLine()
    {
        // The compaction pin renders without a working-tree read; silence beats
        // claiming a clean tree the framework never looked at.
        WorkingStateSection.Build([])
            .Should().NotContain("Changed files in the working tree");
    }

    [Fact]
    public void ReengageNudge_CarriesTheChangedPaths()
    {
        var nudge = MasterNudges.BuildReengageNudge(
            "original task", new ProgressLedger([]), [], ["src/Api.cs"]);

        nudge.Should().Contain("Changed files in the working tree (1): src/Api.cs");
    }
}
