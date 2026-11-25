using AgentSmith.Contracts.Series;
using AgentSmith.Server.Services.Series;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using AgentSmith.Tests.TestSupport;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-25-9992g: a wake is recorded, stamped, on every active or paused series of the project that holds the
/// ticket — once, however often it is delivered — and on no other; the answer says whether one holds it.</summary>
public sealed class SeriesWakesTests
{
    private readonly InMemorySeriesRunStore _store = new();
    private readonly SettableClock _clock = new() { Now = SeriesRecords.T };

    private SeriesWakes Wakes => new(_store, _clock, NullLogger<SeriesWakes>.Instance);

    [Fact]
    public async Task WakeAsync_HoldingSeries_AppendsTheStampedWakeOnce()
    {
        await _store.OpenAsync(SeriesRecords.Running(), default);
        await _store.OpenAsync(SeriesRecords.Running("sr-2") with { State = SeriesRunStates.Paused }, default);
        var wake = new SeriesWake(SeriesWakeKinds.Comment, "please rename it", "alice");

        (await Wakes.WakeAsync("sample", "43", wake, default)).Should().BeTrue();
        _clock.Now = SeriesRecords.T.AddMinutes(1);
        await Wakes.WakeAsync("sample", "43", wake, default); // delivered twice

        foreach (var id in new[] { "sr-1", "sr-2" })
            _store.Records[id].Wakes.Should().ContainSingle()
                .Which.Should().Be(wake with { At = SeriesRecords.T, TicketId = "43" });
    }

    [Fact]
    public async Task WakeAsync_NoSeriesHoldsTheTicket_RecordsNothing()
    {
        await _store.OpenAsync(SeriesRecords.Running(), default);
        await _store.OpenAsync(SeriesRecords.Running("sr-2") with { State = SeriesRunStates.Stopped }, default);

        (await Wakes.WakeAsync("sample", "99", new SeriesWake(SeriesWakeKinds.Run, "ended"), default)).Should().BeFalse();
        (await Wakes.WakeAsync("other", "43", new SeriesWake(SeriesWakeKinds.Run, "ended"), default)).Should().BeFalse();

        _store.Records.Values.Should().OnlyContain(r => r.Wakes.Count == 0);
    }

    [Fact]
    public async Task WakeAsync_AOnceKey_IsRaisedOnce_EvenAfterItsWakeWasCleared()
    {
        await _store.OpenAsync(SeriesRecords.Running(), default);
        var ended = new SeriesWake(SeriesWakeKinds.Run, "its run run-43 ended success") { Once = "run:run-43:success" };

        (await Wakes.WakeAsync("sample", "43", ended, default)).Should().BeTrue();
        _store.Records["sr-1"] = _store.Records["sr-1"] with { Wakes = [] }; // the master judged it
        (await Wakes.WakeAsync("sample", "43", ended, default)).Should().BeTrue("the series still holds the ticket");

        _store.Records["sr-1"].Wakes.Should().BeEmpty("a merge-green act's end re-reads the same attempt");
        _store.Records["sr-1"].Raised.Should().Equal("run:run-43:success");
    }

    [Fact]
    public void SameAs_IgnoresOnlyTheTime()
    {
        var wake = new SeriesWake(SeriesWakeKinds.Ci, "green") { TicketId = "43", At = SeriesRecords.T };

        wake.SameAs(wake with { At = SeriesRecords.T.AddHours(1) }).Should().BeTrue();
        wake.SameAs(wake with { Text = "red" }).Should().BeFalse();
    }

    [Fact]
    public void BudgetUsd_IsTheRaisedOneElseTheConfigured()
    {
        SeriesRecords.Running().BudgetUsd(20m).Should().Be(20m);
        (SeriesRecords.Running() with { BudgetRaisedUsd = 20m }).BudgetUsd(25m).Should().Be(45m);
    }
}
