using AgentSmith.Contracts.Events;
using FluentAssertions;

namespace AgentSmith.Tests.Events;

/// <summary>2026-11-17-7c9bc: the ledger sums every call added from any thread, plus what earlier segments carried.</summary>
public sealed class RecordedCallLedgerTests
{
    [Fact]
    public async Task Add_FromManyThreads_SumsExactly()
    {
        var ledger = new RecordedCallLedger();

        await Task.WhenAll(Enumerable.Range(0, 400).Select(_ => Task.Run(() =>
            ledger.Add(new RecordedCall("m", "Plan", null, 1, 1, 0, 0, 0.01m, IsWorker: true)))));

        ledger.Calls.Should().HaveCount(400);
        ledger.CostUsd.Should().Be(4.00m);
    }

    [Fact]
    public void CostUsd_WithACarry_AddsTheEarlierSegments()
    {
        var ledger = new RecordedCallLedger();
        ledger.Add(new RecordedCall("m", null, null, 1, 1, 0, 0, 0.42m, IsWorker: true));

        ledger.Carry(new RecordedCallCarry(10, 10, 0, 0, 1.00m));

        ledger.CostUsd.Should().Be(1.42m);
        ledger.Calls.Should().ContainSingle("a carry is not a call");
    }
}
