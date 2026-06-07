using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// p0246b: the DB-backed single-run lease + the positive-evidence reaper, proven
/// on a REAL SQLite engine. The lease's UNIQUE(Project,TicketId) index is the
/// claim guard; the reaper only releases a stale lease when the liveness probe
/// returns POSITIVE EVIDENCE the run is gone — never on a stale heartbeat alone.
/// </summary>
public sealed class ActiveRunLeaseTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MutableTimeProvider _clock = new() { Now = DateTimeOffset.Parse("2026-06-07T00:00:00Z") };

    public ActiveRunLeaseTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using var ctx = new AgentSmithDbContext(Options());
        ctx.Database.Migrate();
    }

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    private DbActiveRunLease NewLease() =>
        new(new SharedConnectionContextFactory(_connection),
            new SqliteUniqueViolationTranslator(), _clock);

    [Fact]
    public async Task TryClaim_SecondForSameTicket_ReturnsAlreadyClaimed_OnlyOneRow()
    {
        var lease = NewLease();

        var first = await lease.TryClaimAsync("proj", new TicketId("T-1"), CancellationToken.None);
        var second = await lease.TryClaimAsync("proj", new TicketId("T-1"), CancellationToken.None);

        first.Should().Be(LeaseClaimOutcome.Claimed);
        second.Should().Be(LeaseClaimOutcome.AlreadyClaimed, "the UNIQUE index rejects the duplicate");
        using var ctx = new AgentSmithDbContext(Options());
        ctx.ActiveRuns.Count(a => a.Project == "proj" && a.TicketId == "T-1").Should().Be(1);
    }

    [Fact]
    public async Task Release_AfterClaim_TicketIsReclaimable()
    {
        var lease = NewLease();
        await lease.TryClaimAsync("proj", new TicketId("T-1"), CancellationToken.None);

        await lease.ReleaseAsync("proj", new TicketId("T-1"), CancellationToken.None);
        var reclaim = await lease.TryClaimAsync("proj", new TicketId("T-1"), CancellationToken.None);

        reclaim.Should().Be(LeaseClaimOutcome.Claimed, "releasing the lease frees the ticket");
    }

    [Fact]
    public async Task Reaper_JobCrashesBeforeRelease_ReleasesOnPositiveEvidence_TicketReclaimable()
    {
        var lease = NewLease();
        await lease.TryClaimAsync("proj", new TicketId("T-1"), CancellationToken.None);
        _clock.Now = _clock.Now.AddMinutes(10); // heartbeat goes stale

        var reaper = new ActiveRunReaper(
            lease, new StubProbe(present: false), NullLogger<ActiveRunReaper>.Instance);
        var released = await reaper.RunOnceAsync(TimeSpan.FromMinutes(5), CancellationToken.None);

        released.Should().Be(1, "positive evidence the container is gone releases the crashed lease");
        var reclaim = await lease.TryClaimAsync("proj", new TicketId("T-1"), CancellationToken.None);
        reclaim.Should().Be(LeaseClaimOutcome.Claimed, "the ticket is no longer deadlocked");
    }

    [Fact]
    public async Task Reaper_StaleHeartbeatButContainerAlive_DoesNotRelease()
    {
        var lease = NewLease();
        await lease.TryClaimAsync("proj", new TicketId("T-1"), CancellationToken.None);
        _clock.Now = _clock.Now.AddMinutes(10);

        var reaper = new ActiveRunReaper(
            lease, new StubProbe(present: true), NullLogger<ActiveRunReaper>.Instance);
        var released = await reaper.RunOnceAsync(TimeSpan.FromMinutes(5), CancellationToken.None);

        released.Should().Be(0, "a stale heartbeat with a live container must NOT release the lease");
        var reclaim = await lease.TryClaimAsync("proj", new TicketId("T-1"), CancellationToken.None);
        reclaim.Should().Be(LeaseClaimOutcome.AlreadyClaimed, "the lease is still held");
    }

    private sealed class StubProbe(bool present) : IRunLivenessProbe
    {
        public Task<bool> IsRunPresentAsync(StaleLease lease, CancellationToken cancellationToken)
            => Task.FromResult(present);
    }

    private sealed class SharedConnectionContextFactory(SqliteConnection connection)
        : IDbContextFactory<AgentSmithDbContext>
    {
        public AgentSmithDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(connection).Options);
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; }
        public override DateTimeOffset GetUtcNow() => Now;
    }

    public void Dispose() => _connection.Dispose();
}
