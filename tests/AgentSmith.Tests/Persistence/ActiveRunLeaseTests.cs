using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
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

    // p0246g: the lease logic now lives in ActiveRunRepository (over a unit of
    // work). This test double creates a fresh repository per operation — the same
    // "scope per operation" the production facade does, without the DI scope.
    private IActiveRunLease NewLease() =>
        new RepositoryLease(new SharedConnectionContextFactory(_connection),
            new SqliteUniqueViolationTranslator(), _clock);

    private sealed class RepositoryLease(
        IDbContextFactory<AgentSmithDbContext> factory,
        IUniqueViolationTranslator translator,
        TimeProvider clock) : IActiveRunLease
    {
        public Task<LeaseClaimOutcome> TryClaimAsync(string p, TicketId t, CancellationToken ct)
            => InRepo(r => r.TryClaimAsync(p, t, ct));
        public Task ReleaseAsync(string p, TicketId t, CancellationToken ct)
            => InRepo(r => r.ReleaseAsync(p, t, ct));
        public Task AttachRunAsync(string p, TicketId t, string runId, string? jobId, CancellationToken ct)
            => InRepo(r => r.AttachRunAsync(p, t, runId, jobId, ct));
        public Task RenewHeartbeatAsync(string p, TicketId t, CancellationToken ct)
            => InRepo(r => r.RenewHeartbeatAsync(p, t, ct));
        public Task<IReadOnlyList<StaleLease>> FindStaleAsync(TimeSpan olderThan, CancellationToken ct)
            => InRepo(r => r.FindStaleAsync(olderThan, ct));
        public Task<StaleLease?> GetByTicketAsync(string p, TicketId t, CancellationToken ct)
            => InRepo(r => r.GetByTicketAsync(p, t, ct));
        public Task<IReadOnlyCollection<string>> GetActiveRunIdsAsync(TimeSpan freshFor, CancellationToken ct)
            => InRepo(r => r.GetActiveRunIdsAsync(freshFor, ct));

        private async Task<TResult> InRepo<TResult>(Func<ActiveRunRepository, Task<TResult>> op)
        {
            await using var ctx = factory.CreateDbContext();
            return await op(new ActiveRunRepository(ctx, translator, clock));
        }
        private async Task InRepo(Func<ActiveRunRepository, Task> op)
        {
            await using var ctx = factory.CreateDbContext();
            await op(new ActiveRunRepository(ctx, translator, clock));
        }
    }

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
    public async Task TryClaim_ExistingLeaseStale_ReclaimsInPlace_ClaimedOnlyOneRow()
    {
        // p0258: the run that held the lease died without releasing it (crash /
        // server restart — the heartbeat pump stopped). A re-claim past the
        // staleness window must TAKE OVER the dead lease, not refuse forever (the
        // "stuck on pending, no job in the UI since relational" regression — the
        // DB lease has no TTL, the old Redis 2-min TTL self-healed).
        var lease = NewLease();
        await lease.TryClaimAsync("proj", new TicketId("T-1"), CancellationToken.None);
        await lease.AttachRunAsync("proj", new TicketId("T-1"), "dead-run", jobId: "dead-job", CancellationToken.None);
        _clock.Now = _clock.Now.AddMinutes(4); // 4 missed renewals — the holder is gone

        var reclaim = await lease.TryClaimAsync("proj", new TicketId("T-1"), CancellationToken.None);

        reclaim.Should().Be(LeaseClaimOutcome.Claimed, "a stale lease (dead run) is reclaimable at claim time");
        var row = await lease.GetByTicketAsync("proj", new TicketId("T-1"), CancellationToken.None);
        row!.RunId.Should().BeNull("the reclaim clears the dead run's id — the new run attaches its own");
        row.HeartbeatAt.Should().Be(_clock.Now, "the reclaimed lease is fresh");
        using var ctx = new AgentSmithDbContext(Options());
        ctx.ActiveRuns.Count(a => a.Project == "proj" && a.TicketId == "T-1").Should().Be(1, "reclaim reuses the row");
    }

    [Fact]
    public async Task TryClaim_ExistingLeaseFresh_StillBlocks_SingleRunPreserved()
    {
        // The guard still holds for a LIVE run: a fresh heartbeat (the pump is
        // renewing) must reject the duplicate — reclaim only steals DEAD leases.
        var lease = NewLease();
        await lease.TryClaimAsync("proj", new TicketId("T-1"), CancellationToken.None);
        await lease.AttachRunAsync("proj", new TicketId("T-1"), "live-run", jobId: "live-job", CancellationToken.None);
        _clock.Now = _clock.Now.AddSeconds(90); // within the staleness window — run is alive

        var second = await lease.TryClaimAsync("proj", new TicketId("T-1"), CancellationToken.None);

        second.Should().Be(LeaseClaimOutcome.AlreadyClaimed, "a live run's lease must not be stolen");
        var row = await lease.GetByTicketAsync("proj", new TicketId("T-1"), CancellationToken.None);
        row!.RunId.Should().Be("live-run", "the live run keeps its lease");
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
    public async Task AttachRun_NoExistingLease_InsertsLease_DirectSpawnIsLeaseBacked()
    {
        // p0252: a direct-spawn run (PR-comment / legacy path) never claims, so no
        // lease row exists. AttachRun must UPSERT one — otherwise a live but
        // leaseless run looks dead to StaleJobDetector. Insert-if-absent makes the
        // lease the single liveness source for EVERY in-flight run.
        var lease = NewLease();

        await lease.AttachRunAsync("proj", new TicketId("T-1"), "run-7", jobId: "job-7", CancellationToken.None);

        var row = await lease.GetByTicketAsync("proj", new TicketId("T-1"), CancellationToken.None);
        row.Should().NotBeNull();
        row!.RunId.Should().Be("run-7");
        row.HeartbeatAt.Should().Be(_clock.Now, "the inserted lease is fresh");
        using var ctx = new AgentSmithDbContext(Options());
        ctx.ActiveRuns.Count(a => a.Project == "proj" && a.TicketId == "T-1").Should().Be(1);
    }

    [Fact]
    public async Task AttachRun_ExistingLease_UpdatesInPlace_NoSecondRow()
    {
        // The claimed path: TryClaim INSERTed the row; AttachRun must UPDATE it
        // (set run id + renew heartbeat), never insert a duplicate.
        var lease = NewLease();
        await lease.TryClaimAsync("proj", new TicketId("T-1"), CancellationToken.None);

        await lease.AttachRunAsync("proj", new TicketId("T-1"), "run-7", jobId: "job-7", CancellationToken.None);

        var row = await lease.GetByTicketAsync("proj", new TicketId("T-1"), CancellationToken.None);
        row!.RunId.Should().Be("run-7");
        using var ctx = new AgentSmithDbContext(Options());
        ctx.ActiveRuns.Count(a => a.Project == "proj" && a.TicketId == "T-1").Should().Be(1);
    }

    [Fact]
    public async Task Reaper_StaleHeartbeat_ReleasesLease_TicketReclaimable()
    {
        // p0258: the DB heartbeat is the failure detector. A heartbeat older than
        // the threshold = the owning replica is dead (the pump stopped) → release.
        // No probe: the orphaned sandbox is irrelevant.
        var lease = NewLease();
        await lease.TryClaimAsync("proj", new TicketId("T-1"), CancellationToken.None);
        _clock.Now = _clock.Now.AddMinutes(10); // heartbeat goes stale

        var reaper = new ActiveRunReaper(lease, NullLogger<ActiveRunReaper>.Instance);
        var released = await reaper.RunOnceAsync(TimeSpan.FromMinutes(5), CancellationToken.None);

        released.Should().Be(1, "a stale DB heartbeat means the owning replica is gone");
        var reclaim = await lease.TryClaimAsync("proj", new TicketId("T-1"), CancellationToken.None);
        reclaim.Should().Be(LeaseClaimOutcome.Claimed, "the ticket is no longer deadlocked");
    }

    [Fact]
    public async Task Reaper_FreshHeartbeat_DoesNotRelease()
    {
        // A live run renews its heartbeat (the pump, every 45s) — its lease must
        // never be reaped. Multi-replica-safe: only a dead replica's lease ages out.
        var lease = NewLease();
        await lease.TryClaimAsync("proj", new TicketId("T-1"), CancellationToken.None);
        _clock.Now = _clock.Now.AddMinutes(1); // within the threshold — run is alive

        var reaper = new ActiveRunReaper(lease, NullLogger<ActiveRunReaper>.Instance);
        var released = await reaper.RunOnceAsync(TimeSpan.FromMinutes(5), CancellationToken.None);

        released.Should().Be(0, "a fresh heartbeat is a live run — never reap it");
        var reclaim = await lease.TryClaimAsync("proj", new TicketId("T-1"), CancellationToken.None);
        reclaim.Should().Be(LeaseClaimOutcome.AlreadyClaimed, "the lease is still held");
    }

    [Fact]
    public async Task AttachRun_LinksRunId_GetByTicketReturnsIt()
    {
        // p0242: the run id is attached onto the lease once the run starts, so the
        // stale-revert path can resolve WHICH run to cancel for a ticket.
        var lease = NewLease();
        await lease.TryClaimAsync("proj", new TicketId("T-1"), CancellationToken.None);

        await lease.AttachRunAsync("proj", new TicketId("T-1"), "run-7", jobId: null, CancellationToken.None);
        var found = await lease.GetByTicketAsync("proj", new TicketId("T-1"), CancellationToken.None);

        found.Should().NotBeNull();
        found!.RunId.Should().Be("run-7");
    }

    [Fact]
    public async Task Reaper_NullJobStaleHeartbeat_IsReaped()
    {
        // Regression: an in-process / pre-spawn run (no job handle) that crashed
        // without releasing leaves a null-job lease with a stale heartbeat. It used
        // to be pinned "present forever" by the probe; now a stale heartbeat alone
        // reaps it — the job handle is irrelevant, only the heartbeat matters.
        var lease = NewLease();
        await lease.TryClaimAsync("proj", new TicketId("T-1"), CancellationToken.None);
        await lease.AttachRunAsync("proj", new TicketId("T-1"), "run-7", jobId: null, CancellationToken.None);
        _clock.Now = _clock.Now.AddMinutes(10); // heartbeat goes stale, no renewal

        var reaper = new ActiveRunReaper(lease, NullLogger<ActiveRunReaper>.Instance);
        var released = await reaper.RunOnceAsync(TimeSpan.FromMinutes(5), CancellationToken.None);

        released.Should().Be(1, "a null-job stale lease is a dead run — reapable, not pinned");
        var reclaim = await lease.TryClaimAsync("proj", new TicketId("T-1"), CancellationToken.None);
        reclaim.Should().Be(LeaseClaimOutcome.Claimed, "the ticket is reclaimable after reaping the leak");
    }

    [Fact]
    public async Task GetActiveRunIds_ReturnsFreshLeaseRunIds_ExcludesStale()
    {
        // p0242: the flush-proof source the SandboxOrphanReaper unions in — a live
        // run's id is returned (its sandboxes are spared even on an empty Redis);
        // a stale lease's id is not (its sandboxes are reapable).
        var lease = NewLease();
        await lease.TryClaimAsync("proj", new TicketId("fresh"), CancellationToken.None);
        await lease.AttachRunAsync("proj", new TicketId("fresh"), "run-fresh", null, CancellationToken.None);
        await lease.TryClaimAsync("proj", new TicketId("stale"), CancellationToken.None);
        await lease.AttachRunAsync("proj", new TicketId("stale"), "run-stale", null, CancellationToken.None);
        // Advance so BOTH attach-stamps age, then renew only the fresh one.
        _clock.Now = _clock.Now.AddMinutes(10);
        await lease.RenewHeartbeatAsync("proj", new TicketId("fresh"), CancellationToken.None);

        var active = await lease.GetActiveRunIdsAsync(TimeSpan.FromMinutes(3), CancellationToken.None);

        active.Should().Contain("run-fresh").And.NotContain("run-stale");
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
