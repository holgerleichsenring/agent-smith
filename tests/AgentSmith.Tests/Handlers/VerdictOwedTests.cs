using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Progress;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Handlers;

// 2026-09-08-805f: the handler-side state of the ledger-complete brake.
public sealed class VerdictOwedTests
{
    private static ProgressLedger Ledger(params ProgressStatus[] statuses) =>
        new(statuses.Select((s, i) => new ProgressLedgerEntry((i + 1).ToString(), $"step {i + 1}", s)).ToList());

    private static VerdictOwed Owed(ProgressLedger ledger, decimal spent = 0m) =>
        new("coding-agent-master", () => ledger, () => spent, allowance: 3, NullLogger.Instance);

    [Fact]
    public void IsLedgerComplete_EmptyLedger_False() =>
        Owed(ProgressLedger.Empty).IsLedgerComplete()
            .Should().BeFalse("a run that planned nothing owes this brake nothing");

    [Fact]
    public void IsLedgerComplete_EveryItemDone_True() =>
        Owed(Ledger(ProgressStatus.Done, ProgressStatus.Done)).IsLedgerComplete().Should().BeTrue();

    [Fact]
    public void IsLedgerComplete_AnItemPendingOrInProgress_False()
    {
        Owed(Ledger(ProgressStatus.Done, ProgressStatus.Pending)).IsLedgerComplete().Should().BeFalse();
        Owed(Ledger(ProgressStatus.Done, ProgressStatus.InProgress)).IsLedgerComplete().Should().BeFalse();
    }

    [Fact]
    public void RenderDemand_MarksTheVerdictDemanded()
    {
        var owed = Owed(Ledger(ProgressStatus.Done));

        owed.Demanded.Should().BeFalse();
        var demand = owed.RenderDemand();

        owed.Demanded.Should().BeTrue();
        demand.Should().Contain("checklist is complete");
        demand.Should().Contain("ONLY your final fenced ```verdict block");
        demand.Should().Contain("step 1", "the demand carries the completed checklist");
    }
}
