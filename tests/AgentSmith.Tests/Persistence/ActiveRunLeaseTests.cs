using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Sandbox;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

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
    public async Task Reaper_InProcessRunNullJob_StaleHeartbeat_IsReaped_RealProbe()
    {
        // p0242 regression: an IN-PROCESS run (no orchestrator job) that crashed
        // without releasing leaves a null-job lease with a stale heartbeat. The
        // REAL probe must treat it as gone (was "present forever" — the leak that
        // permanently blocked a ticket after its first run).
        var lease = NewLease();
        await lease.TryClaimAsync("proj", new TicketId("T-1"), CancellationToken.None);
        await lease.AttachRunAsync("proj", new TicketId("T-1"), "run-7", jobId: null, CancellationToken.None);
        _clock.Now = _clock.Now.AddMinutes(10); // heartbeat goes stale, no renewal

        var probe = new OrchestratorRunLivenessProbe(
            new Mock<IJobSpawner>().Object, NullLogger<OrchestratorRunLivenessProbe>.Instance);
        var reaper = new ActiveRunReaper(lease, probe, NullLogger<ActiveRunReaper>.Instance);
        var released = await reaper.RunOnceAsync(TimeSpan.FromMinutes(5), CancellationToken.None);

        released.Should().Be(1, "a null-job stale lease is a dead in-process run — reapable, not pinned");
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
