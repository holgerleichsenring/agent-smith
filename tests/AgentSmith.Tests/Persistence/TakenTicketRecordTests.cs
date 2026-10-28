using AgentSmith.Tests.TestSupport;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
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
        using (var ctx = Context()) MigratedStoreTemplate.CopyInto(ctx);
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
    public async Task ListReconcilable_ATicketParkedOnAQuestion_IsLeftToItsAnswer()
    {
        // 2026-10-28-7d25: a park gives up its lease and keeps the record, so without this the
        // reconciler re-enqueued the ticket every ten minutes and each attempt superseded the last.
        var store = Store();
        await store.TakeAsync(Fact, default);
        AddRun("2026-10-28T08-27-40-a8e1", RunStatuses.WaitingForInput, cancelRequested: false);

        (await store.ListReconcilableAsync(default)).Should().BeEmpty(
            "the ticket waits on a person, and the answer relaunches it");
    }

    [Fact]
    public async Task ListReconcilable_AParkThatWasCancelled_HandsTheRecordBack()
    {
        var store = Store();
        await store.TakeAsync(Fact, default);
        AddRun("2026-10-28T08-27-40-a8e1", RunStatuses.WaitingForInput, cancelRequested: true);

        (await store.ListReconcilableAsync(default)).Should().ContainSingle()
            .Which.Should().Be(Fact);
    }

    [Fact]
    public async Task ListReconcilable_ANewerUnparkedAttempt_IsReconcilable()
    {
        // An older park the superseded closer has not reached yet must not hide an attempt that crashed.
        var store = Store();
        await store.TakeAsync(Fact, default);
        AddRun("2026-10-28T08-27-40-a8e1", RunStatuses.WaitingForInput, cancelRequested: false);
        AddRun("2026-10-28T08-38-15-b2c4", "running", cancelRequested: false);

        (await store.ListReconcilableAsync(default)).Should().ContainSingle();
    }

    [Fact]
    public async Task ListReconcilable_AParkedDerivation_DoesNotHideTheCodeRecord()
    {
        var store = Store();
        await store.TakeAsync(Fact, default);
        AddRun("2026-10-28T08-27-40-a8e1", RunStatuses.WaitingForInput, cancelRequested: false, pipeline: "spec-derive");

        (await store.ListReconcilableAsync(default)).Should().ContainSingle(
            "the record belongs to the pipeline that took the ticket up");
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

    private void AddRun(string runId, string status, bool cancelRequested, string? pipeline = null)
    {
        using var ctx = Context();
        ctx.Add(new Run
        {
            Id = runId, Project = Fact.Project, TicketId = Fact.TicketId, Pipeline = pipeline ?? Fact.Pipeline,
            Status = status, CancelRequested = cancelRequested, StartedAt = DateTimeOffset.UtcNow,
        });
        ctx.SaveChanges();
    }

    private ITakenTicketStore Store() => _services.GetRequiredService<ITakenTicketStore>();

    private AgentSmithDbContext Context() => new(
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
}
