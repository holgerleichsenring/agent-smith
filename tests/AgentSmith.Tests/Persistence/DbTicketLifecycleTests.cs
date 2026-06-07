using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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

    private DbTicketLifecycleStore NewStore() => new(new Factory(_connection));

    private DbAuthoritativeTicketStatusTransitioner Authoritative(ITicketStatusTransitioner inner) =>
        new(inner, NewStore(), project: "", platform: "GitHub",
            NullLogger<DbAuthoritativeTicketStatusTransitioner>.Instance);

    [Fact]
    public async Task Lifecycle_DriftBetweenDbAndLabel_DbWins()
    {
        // The DB says InProgress; the platform label drifted back to Pending (the
        // stale detector reverted it). The authoritative read returns the DB value.
        await NewStore().SetStatusAsync("", "GitHub", new TicketId("42"),
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
        var dbStatus = await NewStore().GetStatusAsync("", "GitHub", new TicketId("42"), CancellationToken.None);
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

    private sealed class StubTransitioner(
        TicketLifecycleStatus? label = null, bool throwOnTransition = false) : ITicketStatusTransitioner
    {
        public string ProviderType => "stub";

        public Task<TicketLifecycleStatus?> ReadCurrentAsync(TicketId ticketId, CancellationToken ct)
            => Task.FromResult(label);

        public Task<TransitionResult> TransitionAsync(
            TicketId ticketId, TicketLifecycleStatus from, TicketLifecycleStatus to, CancellationToken ct)
            => throwOnTransition
                ? throw new InvalidOperationException("label API down")
                : Task.FromResult(TransitionResult.Succeeded());
    }

    private sealed class Factory(SqliteConnection connection) : IDbContextFactory<AgentSmithDbContext>
    {
        public AgentSmithDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(connection).Options);
    }

    public void Dispose() => _connection.Dispose();
}
