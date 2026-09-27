using AgentSmith.Application.Services.Persistence;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-09-25-b4d9: the taken-ticket record against a real store. The relational one is the
/// one that matters — it is the half that outlives the process a crash takes down — but the
/// DB-free store answers the same questions, so both run the same sequence.
/// </summary>
public sealed class TakenTicketRecordTests : IDisposable
{
    private static readonly TakenTicketFact Fact = new("proj", "42", "github", "migrate-repo");

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly ServiceProvider _services;

    public TakenTicketRecordTests()
    {
        _connection.Open();
        using (var ctx = Context()) ctx.Database.Migrate();
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => Context());
        services.AddTicketFactStores();
        _services = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _services.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void AddTicketFactStores_BindsTheRelationalStore() =>
        Store().Should().BeOfType<DbTakenTicketStore>(
            "the in-memory default only survives the process it runs in");

    [Fact]
    public async Task Take_TheSameTicketTwice_KeepsOneRecordWithTheLatestClaim()
    {
        var store = Store();
        await store.TakeAsync(Fact, default);
        await store.TakeAsync(Fact with { Pipeline = "fix-bug" }, default);

        (await store.ListReconcilableAsync(default)).Should().ContainSingle()
            .Which.Pipeline.Should().Be("fix-bug");
        using var ctx = Context();
        ctx.TakenTickets.Count().Should().Be(1, "the UNIQUE index is per (project, ticket)");
    }

    [Fact]
    public async Task Clear_ATicketThatFinished_DropsTheRecord()
    {
        var store = Store();
        await store.TakeAsync(Fact, default);

        await store.ClearAsync("proj", "42", default);

        (await store.ListReconcilableAsync(default)).Should().BeEmpty();
    }

    [Fact]
    public async Task TryBeginReap_ARecordAnotherPassHolds_IsRefused()
    {
        var store = Store();
        await store.TakeAsync(Fact, default);

        (await store.TryBeginReapAsync("proj", "42", default)).Should().BeTrue();
        (await store.TryBeginReapAsync("proj", "42", default)).Should().BeFalse();
        (await store.ListReconcilableAsync(default)).Should().BeEmpty(
            "the reaper owns the ticket until it is done with it");

        await store.EndReapAsync("proj", "42", default);
        (await store.ListReconcilableAsync(default)).Should().ContainSingle();
    }

    [Fact]
    public async Task TryBeginReap_ATicketWithNoRecord_IsNobodysToHold() =>
        // A lease from before this record existed, or one whose ticket already finished:
        // there is nothing to coordinate, so the reap proceeds.
        (await Store().TryBeginReapAsync("proj", "unknown", default)).Should().BeTrue();

    [Fact]
    public async Task TheDbFreeStore_AnswersTheSameQuestions()
    {
        ITakenTicketStore store = new InMemoryTakenTicketStore();
        await store.TakeAsync(Fact, default);

        (await store.TryBeginReapAsync("proj", "42", default)).Should().BeTrue();
        (await store.TryBeginReapAsync("proj", "42", default)).Should().BeFalse();
        (await store.ListReconcilableAsync(default)).Should().BeEmpty();
        await store.EndReapAsync("proj", "42", default);
        (await store.ListReconcilableAsync(default)).Should().ContainSingle()
            .Which.Should().Be(Fact);

        await store.ClearAsync("proj", "42", default);
        (await store.ListReconcilableAsync(default)).Should().BeEmpty();
        (await store.TryBeginReapAsync("proj", "42", default)).Should().BeTrue();
    }

    private ITakenTicketStore Store() => _services.GetRequiredService<ITakenTicketStore>();

    private AgentSmithDbContext Context() => new(
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
}
