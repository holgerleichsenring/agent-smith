using AgentSmith.Contracts.Series;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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

    [Fact]
    public async Task SeriesRunStore_Paused_IsActiveAndHoldsItsTicketsOnly()
    {
        await Store.OpenAsync(SeriesRecords.Running("sr-1", "conv-1"), default);
        (await Store.PausedWithTicketAsync("sample", "44", default)).Should().BeNull("a running series holds nothing");

        await Store.UpdateAsync("sr-1", r => r with { State = SeriesRunStates.Paused, PausedFrom = SeriesRunStates.Running }, default);

        (await Store.PausedWithTicketAsync("sample", "44", default))!.Id.Should().Be("sr-1");
        (await Store.PausedWithTicketAsync("sample", "99", default)).Should().BeNull("not a ticket of the series");
        (await Store.PausedWithTicketAsync("other", "44", default)).Should().BeNull("another project's ticket 44");
        (await Store.ActiveForConversationAsync("conv-1", default))!.PausedFrom.Should().Be(SeriesRunStates.Running);
        (await Store.RunningWithTicketAsync("sample", "44", default)).Should().BeNull("a paused series is not running");

        await Store.UpdateAsync("sr-1", r => r with { State = SeriesRunStates.Running, PausedFrom = null }, default);
        (await Store.RunningWithTicketAsync("sample", "44", default))!.Id.Should().Be("sr-1");
    }

    [Fact]
    public async Task ListAsync_NamedProjects_AnswersActiveAndEndedNewestActivityFirst()
    {
        await Store.OpenAsync(SeriesRecords.Running("sr-old", "conv-1"), default);
        await Store.OpenAsync(SeriesRecords.Running("sr-new", "conv-2") with { StartedAt = SeriesRecords.T.AddHours(1) }, default);
        await Store.OpenAsync(SeriesRecords.Running("sr-other", "conv-3") with { Project = "other" }, default);
        await Store.UpdateAsync("sr-old", r => r with
        {
            State = SeriesRunStates.Done, FinishedAt = SeriesRecords.T.AddHours(2),
        }, default);

        (await Store.ListAsync(["sample"], default)).Select(r => r.Id).Should().Equal(["sr-old", "sr-new"],
            "the ended series moved last; another project's series is not listed");
        (await Store.ListAsync(null, default)).Should().HaveCount(3);
    }

    [RequiresPostgresFact]
    public async Task ListAsync_OnPostgres_FiltersByProject()
    {
        await using var db = await ScratchPostgres.MigratedAsync(RequiresPostgresFactAttribute.ConnectionString!, "series");
        try
        {
            var options = ScratchPostgres.Options(db.Database.GetConnectionString()!);
            await using var provider = new ServiceCollection()
                .AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(options)).BuildServiceProvider();
            var store = new SeriesRunRepository(provider.GetRequiredService<IServiceScopeFactory>());
            await store.OpenAsync(SeriesRecords.Running("sr-1", "conv-1"), default);
            await store.OpenAsync(SeriesRecords.Running("sr-2", "conv-2") with { Project = "other" }, default);

            (await store.ListAsync(["sample"], default)).Should().ContainSingle().Which.Id.Should().Be("sr-1");
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    public void Dispose() => _store.Dispose();
}
