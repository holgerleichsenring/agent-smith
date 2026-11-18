using AgentSmith.Contracts.Series;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-17-0207a: a series run is kept over the shipped migrations, found by its conversation while it
/// runs, and two writers of one series keep both their changes.</summary>
public sealed class SeriesRunStoreTests : IDisposable
{
    private readonly ServerStateStore _store = new();

    private SeriesRunRepository Store => new(_store.ScopeFactory);

    [Fact]
    public async Task SeriesRunStore_Open_IsFoundByIdAndConversationWhileActive()
    {
        await Store.OpenAsync(SeriesRecords.Running("sr-1", "conv-1"), default);

        (await Store.GetAsync("sr-1", default))!.Tickets.Should().HaveCount(3);
        (await Store.ActiveForConversationAsync("conv-1", default))!.Id.Should().Be("sr-1");
        (await Store.ActiveAsync(default)).Should().ContainSingle();
    }

    [Fact]
    public async Task SeriesRunStore_Stopped_IsNoLongerActiveButStillTheLatest()
    {
        await Store.OpenAsync(SeriesRecords.Running("sr-1", "conv-1"), default);

        await Store.UpdateAsync("sr-1", r => r with { State = SeriesRunStates.Stopped }, default);

        (await Store.ActiveForConversationAsync("conv-1", default)).Should().BeNull();
        (await Store.LatestForConversationAsync("conv-1", default))!.State.Should().Be(SeriesRunStates.Stopped);
    }

    [Fact]
    public async Task SeriesRunStore_TwoWriters_KeepBothChanges()
    {
        await Store.OpenAsync(SeriesRecords.Running("sr-1", "conv-1"), default);
        var first = true;

        await Store.UpdateAsync("sr-1", r =>
        {
            if (first) { first = false; Store.UpdateAsync("sr-1", o => o with { MasterCostUsd = 2m }, default).GetAwaiter().GetResult(); }
            return r with { BeatRunId = "run-9" };
        }, default);

        var kept = await Store.GetAsync("sr-1", default);
        kept!.MasterCostUsd.Should().Be(2m, "the writer that saved in between is not lost");
        kept.BeatRunId.Should().Be("run-9");
    }

    public void Dispose() => _store.Dispose();
}
