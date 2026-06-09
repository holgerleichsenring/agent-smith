using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// p0246d: the DB is the ticket-lifecycle system-of-record; the platform label
/// is a best-effort projection. On drift the DB wins, and a label write that
/// fails never fails the transition. Proven on a real SQLite engine.
/// </summary>
public sealed class DbTicketLifecycleTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public DbTicketLifecycleTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using var ctx = new AgentSmithDbContext(Options());
        ctx.Database.Migrate();
    }

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    // A repository over a fresh context on the shared connection (direct DB checks).
    private TicketLifecycleRepository NewRepo() => new(new AgentSmithDbContext(Options()));

    // The transitioner opens a scope per op; build a provider whose scoped
    // IUnitOfWork is a fresh context over the same in-memory connection.
    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(Options()));
        services.AddScoped<TicketLifecycleRepository>();
        return services.BuildServiceProvider();
    }

    private DbAuthoritativeTicketStatusTransitioner Authoritative(ITicketStatusTransitioner inner) =>
        new(inner, BuildProvider().GetRequiredService<IServiceScopeFactory>(), project: "", platform: "GitHub",
            NullLogger<DbAuthoritativeTicketStatusTransitioner>.Instance);

    [Fact]
    public async Task Lifecycle_DriftBetweenDbAndLabel_DbWins()
    {
        // The DB says InProgress; the platform label drifted back to Pending (the
        // stale detector reverted it). The authoritative read returns the DB value.
        await NewRepo().SetStatusAsync("", "GitHub", new TicketId("42"),
            TicketLifecycleStatus.InProgress.ToString(), CancellationToken.None);
        var sut = Authoritative(new StubTransitioner(label: TicketLifecycleStatus.Pending));

        var current = await sut.ReadCurrentAsync(new TicketId("42"), CancellationToken.None);

        current.Should().Be(TicketLifecycleStatus.InProgress, "the DB is the system-of-record, it wins on drift");
    }

    [Fact]
    public async Task Lifecycle_LabelProjectionFails_TransitionStillSucceeds_DbUpdated()
    {
        // The platform label update throws (API down). The DB transition must
        // still succeed and record the new status — the label is best-effort.
        var sut = Authoritative(new StubTransitioner(throwOnTransition: true));

        var result = await sut.TransitionAsync(
            new TicketId("42"), TicketLifecycleStatus.Pending, TicketLifecycleStatus.Enqueued, CancellationToken.None);

        result.IsSuccess.Should().BeTrue("a failed label projection must not fail the authoritative DB transition");
        var dbStatus = await NewRepo().GetStatusAsync("", "GitHub", new TicketId("42"), CancellationToken.None);
        dbStatus.Should().Be(TicketLifecycleStatus.Enqueued.ToString(), "the DB recorded the new status");
    }

    [Fact]
    public async Task Lifecycle_NoDbRowYet_FallsBackToLabel()
    {
        // Before any DB transition, the authoritative read falls back to the label.
        var sut = Authoritative(new StubTransitioner(label: TicketLifecycleStatus.Done));

        var current = await sut.ReadCurrentAsync(new TicketId("99"), CancellationToken.None);

        current.Should().Be(TicketLifecycleStatus.Done, "with no DB row, the label is the only signal");
    }

    [Fact]
    public async Task Lifecycle_LabelProjection_ReAnchorsOnActualLabelState_NotCallerFrom()
    {
        // p0258: the run-end path passes from=Pending when it can't read current,
        // but the label is actually in-progress. The projection must re-anchor on
        // the label's REAL state so the terminal write lands — otherwise the
        // failed/done tag never sticks and the ticket falls back to claimable and
        // auto-re-triggers (the "inconsistent tag state / job re-triggers" loop).
        var inner = new StubTransitioner(label: TicketLifecycleStatus.InProgress);
        var sut = Authoritative(inner);

        await sut.TransitionAsync(new TicketId("42"),
            TicketLifecycleStatus.Pending, TicketLifecycleStatus.Failed, CancellationToken.None);

        inner.LastFrom.Should().Be(TicketLifecycleStatus.InProgress,
            "the projection re-anchors on the label's actual state, not the caller's stale from");
    }

    private sealed class StubTransitioner(
        TicketLifecycleStatus? label = null, bool throwOnTransition = false) : ITicketStatusTransitioner
    {
        public string ProviderType => "stub";
        public TicketLifecycleStatus? LastFrom { get; private set; }

        public Task<TicketLifecycleStatus?> ReadCurrentAsync(TicketId ticketId, CancellationToken ct)
            => Task.FromResult(label);

        public Task<TransitionResult> TransitionAsync(
            TicketId ticketId, TicketLifecycleStatus from, TicketLifecycleStatus to, CancellationToken ct)
        {
            LastFrom = from;
            return throwOnTransition
                ? throw new InvalidOperationException("label API down")
                : Task.FromResult(TransitionResult.Succeeded());
        }
    }

    private sealed class Factory(SqliteConnection connection) : IDbContextFactory<AgentSmithDbContext>
    {
        public AgentSmithDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(connection).Options);
    }

    public void Dispose() => _connection.Dispose();
}
