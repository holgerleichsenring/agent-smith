using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Interceptors;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using AgentSmith.Tests.Architecture;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.ModelSlots;

/// <summary>
/// 2026-11-20-24bd: the model places in the database. Concurrent takers at limit 1 are admitted exactly once by the
/// unique (Key, Slot) index — on SQLite here, on SQL Server and PostgreSQL when a server is handed to the suite — a
/// run re-enters its own place, and a terminal projection and a delete free a place a run left behind.
/// </summary>
public sealed class ModelRunSlotStoreTests : IDisposable
{
    private const string Key = "azure_openai|https://shared.openai.azure.com|gpt41";
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"agentsmith-slots-{Guid.NewGuid():N}.db");

    public ModelRunSlotStoreTests() => MigratedStoreTemplate.CopyToFile(_path);

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_path);
    }

    [Fact]
    public async Task ConcurrentAcquire_LimitOne_Sqlite_AdmitsExactlyOne()
    {
        var store = Store(SqliteOptions(), new SqliteUniqueViolationTranslator());

        var admitted = await AcquireAtOnceAsync(store, runs: 8, limit: 1);

        admitted.Should().ContainSingle("the unique (Key, Slot) index admits one taker of the only place");
        (await RowsAsync(SqliteOptions())).Should().ContainSingle().Which.RunId.Should().Be(admitted.Single());
    }

    [Fact]
    public async Task ConcurrentAcquire_LimitTwo_Sqlite_AdmitsExactlyTwoOnDistinctPlaces()
    {
        var store = Store(SqliteOptions(), new SqliteUniqueViolationTranslator());

        var admitted = await AcquireAtOnceAsync(store, runs: 6, limit: 2);

        admitted.Should().HaveCount(2);
        (await RowsAsync(SqliteOptions())).Select(r => r.Slot).Should().BeEquivalentTo([0, 1]);
    }

    [RequiresSqlServerFact]
    public async Task ConcurrentAcquire_LimitOne_SqlServer_AdmitsExactlyOne()
    {
        await using var db = await ScratchSqlServer.MigratedAsync(RequiresSqlServerFactAttribute.ConnectionString!, "slots");
        try
        {
            var options = ProviderOptions(PersistenceProvider.SqlServer, db.Database.GetConnectionString()!);
            var admitted = await AcquireAtOnceAsync(Store(options, new SqlServerUniqueViolationTranslator()), runs: 8, limit: 1);
            admitted.Should().ContainSingle();
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    [RequiresPostgresFact]
    public async Task ConcurrentAcquire_LimitOne_Postgres_AdmitsExactlyOne()
    {
        await using var db = await ScratchPostgres.MigratedAsync(RequiresPostgresFactAttribute.ConnectionString!, "slots");
        try
        {
            var options = ProviderOptions(PersistenceProvider.Postgresql, db.Database.GetConnectionString()!);
            var admitted = await AcquireAtOnceAsync(Store(options, new NpgsqlUniqueViolationTranslator()), runs: 8, limit: 1);
            admitted.Should().ContainSingle();
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task Acquire_TheRunAlreadyHoldingThePlace_IsAdmittedAgainWithoutASecondRow()
    {
        var store = Store(SqliteOptions(), new SqliteUniqueViolationTranslator());
        (await store.AcquireAsync(Key, 1, "run-a", CancellationToken.None)).Should().BeTrue();

        (await store.AcquireAsync(Key, 1, "run-a", CancellationToken.None)).Should().BeTrue("a run never waits on itself");
        (await store.AcquireAsync(Key, 1, "run-b", CancellationToken.None)).Should().BeFalse();
        (await RowsAsync(SqliteOptions())).Should().ContainSingle();
    }

    [Fact]
    public async Task Release_FreesThePlace_AndWakesTheWaitersOfThisProcess()
    {
        var signal = new ModelRunSlotSignal();
        var store = Store(SqliteOptions(), new SqliteUniqueViolationTranslator(), signal);
        await store.AcquireAsync(Key, 1, "run-a", CancellationToken.None);
        signal.Held("run-a", Key, DateTimeOffset.UtcNow);
        var woken = signal.NextRelease;

        await store.ReleaseAsync("run-a", CancellationToken.None);

        woken.IsCompleted.Should().BeTrue();
        signal.Holds("run-a", Key).Should().BeFalse("a released run takes its place again at its next call");
        (await store.AcquireAsync(Key, 1, "run-b", CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task Acquire_ALoweredLimit_AdmitsNobodyUntilEnoughHaveLeft()
    {
        var store = Store(SqliteOptions(), new SqliteUniqueViolationTranslator());
        await store.AcquireAsync(Key, 2, "run-a", CancellationToken.None);
        await store.AcquireAsync(Key, 2, "run-b", CancellationToken.None);

        (await store.AcquireAsync(Key, 1, "run-c", CancellationToken.None)).Should().BeFalse();
        await store.ReleaseAsync("run-a", CancellationToken.None);
        (await store.AcquireAsync(Key, 1, "run-c", CancellationToken.None)).Should().BeFalse("run-b still holds the one place");
        await store.ReleaseAsync("run-b", CancellationToken.None);
        (await store.AcquireAsync(Key, 1, "run-c", CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task Acquire_ARunHoldingAPlaceOnAnotherKey_TakesNoSecond()
    {
        var store = Store(SqliteOptions(), new SqliteUniqueViolationTranslator());
        await store.AcquireAsync("other-key", 1, "run-a", CancellationToken.None);

        (await store.AcquireAsync(Key, 1, "run-a", CancellationToken.None)).Should().BeTrue();

        (await RowsAsync(SqliteOptions())).Should().ContainSingle().Which.Key.Should().Be("other-key");
    }

    [Fact]
    public async Task Sweep_FreesOnlyPlacesNoExecutingRunCanOwn()
    {
        var clock = new ModelSlotsClock();
        var signal = new ModelRunSlotSignal();
        var store = Store(SqliteOptions(), new SqliteUniqueViolationTranslator(), signal, clock);
        var stale = clock.GetUtcNow() - TimeSpan.FromMinutes(10);
        await SeedRunAsync("run-live", startedAt: stale); // running, no recent beat: the liveness reaper's call, not ours
        await SeedRunAsync("run-parked", status: "waiting_for_input", startedAt: stale);
        await SeedRunAsync("run-just-parked", status: "waiting_for_input", heartbeatAt: clock.GetUtcNow());
        await SeedRunAsync("run-ended", status: "success", finished: true);
        foreach (var (run, key) in new[] { ("run-live", "k1"), ("run-parked", "k2"), ("run-just-parked", "k3"), ("run-ended", "k4"), ("run-rowless", "k5") })
            await store.AcquireAsync(key, 1, run, CancellationToken.None);
        await store.WaitStartedAsync("run-live", CancellationToken.None);
        var woken = signal.NextRelease;

        (await store.SweepAsync(TimeSpan.FromMinutes(3), CancellationToken.None)).Should().Be(2, "the parked stale run and the ended one");
        (await RowsAsync(SqliteOptions())).Select(r => r.RunId).Should().BeEquivalentTo(["run-live", "run-just-parked", "run-rowless"]);
        woken.IsCompleted.Should().BeTrue();

        clock.Advance(ModelRunSlotSweep.RowlessGrace + TimeSpan.FromMinutes(1));
        (await store.SweepAsync(TimeSpan.FromMinutes(3), CancellationToken.None)).Should().Be(2, "the rowless place after its grace; run-just-parked has gone stale");
        (await RowsAsync(SqliteOptions())).Select(r => r.RunId).Should().BeEquivalentTo(["run-live"]);
        (await store.WaitedAsync(["run-live"], CancellationToken.None)).Should().ContainKey("run-live");
    }

    // 2026-11-20-9936b: the same sweep releases capacity reservations no run can use — an ended run's, and a rowless one's
    // after the grace — and never a queued, parked or running run's, however stale.
    [Fact]
    public async Task Sweep_ReleasesCapacityOfEndedAndRowlessRuns_KeepsWaitingAndRunningOnes()
    {
        var clock = new ModelSlotsClock();
        var store = Store(SqliteOptions(), new SqliteUniqueViolationTranslator(), clock: clock);
        var stale = clock.GetUtcNow() - TimeSpan.FromHours(2);
        await SeedRunAsync("run-ended", status: "success", finished: true);
        await SeedRunAsync("run-queued", status: "queued", startedAt: stale);
        await SeedRunAsync("run-parked", status: "waiting_for_input", startedAt: stale);
        await SeedRunAsync("run-live", startedAt: stale);
        foreach (var run in new[] { "run-ended", "run-queued", "run-parked", "run-live", "run-rowless" })
            await SeedCapacityAsync(run, reserved: run != "run-queued");
        await SeedCapacityAsync("run-ended-unreserved", reserved: false);
        await SeedRunAsync("run-ended-unreserved", status: "failed", finished: true);

        await store.SweepAsync(TimeSpan.FromMinutes(3), CancellationToken.None);

        (await CapacityRunIdsAsync()).Should().BeEquivalentTo(["run-queued", "run-parked", "run-live", "run-rowless"],
            "an ended run holds nothing; a rowless reservation waits out the grace, its run's row may still be coming");

        clock.Advance(ModelRunSlotSweep.RowlessGrace + TimeSpan.FromMinutes(1));
        await store.SweepAsync(TimeSpan.FromMinutes(3), CancellationToken.None);
        (await CapacityRunIdsAsync()).Should().Contain("run-rowless", "an admitted run waits in the job queue without a row");

        clock.Advance(ModelRunSlotSweep.RowlessReservationGrace);
        await store.SweepAsync(TimeSpan.FromMinutes(3), CancellationToken.None);

        (await CapacityRunIdsAsync()).Should().BeEquivalentTo(["run-queued", "run-parked", "run-live"],
            "queued and waiting_for_input keep their reservation by design, and a running row is the liveness reaper's");
    }

    // The claim takes the lease (no run id yet) and enqueues the job; the Run row appears only when the job dequeues.
    [Fact]
    public async Task Sweep_ClaimedEnqueuedNotStarted_KeepsItsReservation()
    {
        var clock = new ModelSlotsClock();
        var store = Store(SqliteOptions(), new SqliteUniqueViolationTranslator(), clock: clock);
        await SeedCapacityAsync("run-enqueued", reserved: true);
        await SeedLeaseAsync("1", runId: null, claimedAt: DateTimeOffset.UtcNow);

        clock.Advance(ModelRunSlotSweep.RowlessGrace + TimeSpan.FromMinutes(1));
        await store.SweepAsync(TimeSpan.FromMinutes(3), CancellationToken.None);
        (await CapacityRunIdsAsync()).Should().Equal(["run-enqueued"]);

        clock.Advance(ModelRunSlotSweep.RowlessReservationGrace);
        await store.SweepAsync(TimeSpan.FromMinutes(3), CancellationToken.None);
        (await CapacityRunIdsAsync()).Should().Equal(["run-enqueued"], "an unattached lease claimed after it may still be its claim");
    }

    [Theory]
    [InlineData("lease")]
    [InlineData("queue")]
    public async Task Sweep_RowlessReservationALeaseOrQueueEntryNames_IsKept(string holder)
    {
        var clock = new ModelSlotsClock();
        var store = Store(SqliteOptions(), new SqliteUniqueViolationTranslator(), clock: clock);
        await SeedCapacityAsync("run-held", reserved: true);
        await SeedCapacityAsync("run-orphan", reserved: true);
        await using (var db = new AgentSmithDbContext(SqliteOptions()))
        {
            if (holder == "lease")
                db.Add(new ActiveRun { Project = "p", TicketId = "1", RunId = "run-held", ClaimedAt = DateTimeOffset.UtcNow, HeartbeatAt = DateTimeOffset.UtcNow });
            else
                db.Add(new QueuedTicket { Project = "p", TicketId = "1", Pipeline = "code", Platform = "github", ReservedRunId = "run-held", EnqueuedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }

        clock.Advance(ModelRunSlotSweep.RowlessReservationGrace + TimeSpan.FromMinutes(1));
        await store.SweepAsync(TimeSpan.FromMinutes(3), CancellationToken.None);

        (await CapacityRunIdsAsync()).Should().Equal(["run-held"]);
    }

    // The read and the delete are two statements: what changed between them is checked again by the delete.
    [Fact]
    public async Task DeleteReleased_RunRowAppearedOrTurnedLiveOrLeaseTookItUp_KeepsTheRow()
    {
        foreach (var run in new[] { "run-appeared", "run-resumed", "run-leased", "run-ended", "run-orphan" })
            await SeedCapacityAsync(run, reserved: true);
        await SeedRunAsync("run-appeared", status: "running");
        await SeedRunAsync("run-resumed", status: "waiting_for_input");
        await SeedRunAsync("run-ended", status: "failed", finished: true);
        await SeedLeaseAsync("2", runId: "run-leased", claimedAt: DateTimeOffset.UtcNow);

        await using (var db = new AgentSmithDbContext(SqliteOptions()))
            (await ModelRunSlotSweep.DeleteReleasedAsync(db, ["run-appeared", "run-resumed", "run-leased", "run-ended", "run-orphan"], CancellationToken.None))
                .Should().Be(2);

        (await CapacityRunIdsAsync()).Should().BeEquivalentTo(["run-appeared", "run-resumed", "run-leased"]);
    }

    [Fact]
    public async Task Sweep_CapacityLedgerUnreadable_ThrowsOnlyAfterThePlacesAreFreed()
    {
        var store = Store(SqliteOptions(), new SqliteUniqueViolationTranslator());
        await SeedRunAsync("run-ended", status: "success", finished: true);
        await store.AcquireAsync(Key, 1, "run-ended", CancellationToken.None);
        await using (var db = new AgentSmithDbContext(SqliteOptions()))
            await db.Database.ExecuteSqlRawAsync("DROP TABLE RunCapacities");

        var sweep = () => store.SweepAsync(TimeSpan.FromMinutes(3), CancellationToken.None);

        await sweep.Should().ThrowAsync<Exception>("the enforcer's try logs it and goes on to the force-kills");
        (await RowsAsync(SqliteOptions())).Should().BeEmpty("the place was freed before the ledger was read");
    }

    [Fact]
    public async Task Acquire_ClosesTheRunsOpenWait_InTheSameStoreOperation()
    {
        var clock = new ModelSlotsClock();
        var store = Store(SqliteOptions(), new SqliteUniqueViolationTranslator(), clock: clock);
        await store.WaitStartedAsync("run-a", CancellationToken.None);
        clock.Advance(TimeSpan.FromMinutes(10));

        await store.AcquireAsync(Key, 1, "run-a", CancellationToken.None); // the gate's end-of-wait was lost
        clock.Advance(TimeSpan.FromHours(1));

        (await store.WaitedAsync(["run-a"], CancellationToken.None))["run-a"]
            .Should().Be(TimeSpan.FromMinutes(10), "an open wait would keep growing and shield a hung run from the wall-time clocks");
    }

    [Fact]
    public async Task WallTimeOverdue_LeavesOutTheTimeARunWaitedForItsPlace()
    {
        var clock = new ModelSlotsClock();
        var store = Store(SqliteOptions(), new SqliteUniqueViolationTranslator(), clock: clock);
        var now = clock.GetUtcNow();
        await SeedRunAsync("run-waited", startedAt: now - TimeSpan.FromMinutes(40));
        await SeedRunAsync("run-waiting", startedAt: now - TimeSpan.FromMinutes(40));
        await SeedRunAsync("run-busy", startedAt: now - TimeSpan.FromMinutes(40));
        clock.Advance(-TimeSpan.FromMinutes(20));
        await store.WaitStartedAsync("run-waited", CancellationToken.None);
        await store.WaitStartedAsync("run-waiting", CancellationToken.None);
        clock.Advance(TimeSpan.FromMinutes(15));
        await store.WaitEndedAsync("run-waited", CancellationToken.None); // waited 15 minutes, then worked
        clock.Advance(TimeSpan.FromMinutes(5)); // run-waiting still waits: 20 minutes so far

        await using var db = new AgentSmithDbContext(SqliteOptions());
        var overdue = await new RunRepository(db).GetWallTimeOverdueRunsAsync(TimeSpan.FromMinutes(30), now, CancellationToken.None);

        overdue.Select(r => r.Id).Should().BeEquivalentTo(["run-busy"], "40 minutes less 15 or 20 waited is inside a 30-minute ceiling");
    }

    [Theory]
    [InlineData("success")]
    [InlineData("failed")]
    [InlineData("cancelled")]
    public async Task TerminalRunFinished_FreesAPlaceTheRunLeftBehind(string status)
    {
        var store = Store(SqliteOptions(), new SqliteUniqueViolationTranslator());
        await SeedRunAsync("run-crashed");
        await store.AcquireAsync(Key, 1, "run-crashed", CancellationToken.None);

        await store.WaitStartedAsync("run-crashed", CancellationToken.None);
        await RunEventAppliers.Default(modelRunSlots: store).ApplyAsync(
            new AgentSmithDbContext(SqliteOptions()),
            new RunFinishedEvent("run-crashed", status, null, "ended", DateTimeOffset.UtcNow), CancellationToken.None);

        (await RowsAsync(SqliteOptions())).Should().BeEmpty();
        (await store.WaitedAsync(["run-crashed"], CancellationToken.None)).Should().BeEmpty("an ended run's waits are forgotten");
    }

    [Fact]
    public async Task DeletingARun_RemovesItsPlace()
    {
        var store = Store(SqliteOptions(), new SqliteUniqueViolationTranslator());
        await SeedRunAsync("run-deleted");
        await store.AcquireAsync(Key, 1, "run-deleted", CancellationToken.None);

        var signal = new ModelRunSlotSignal();
        signal.Held("run-deleted", Key, DateTimeOffset.UtcNow);
        await using (var db = new AgentSmithDbContext(SqliteOptions()))
            await new RunDeletionRepository(db, modelPlaces: signal).DeleteAsync("run-deleted", CancellationToken.None);

        (await RowsAsync(SqliteOptions())).Should().BeEmpty();
        signal.HeldBy("run-deleted").Should().BeNull("a deleted run's hold is not trusted any longer in this process");
    }

    [Theory]
    [InlineData("AgentSmith.Infrastructure.Persistence")]
    [InlineData("AgentSmith.Infrastructure.Persistence.SqlServer")]
    [InlineData("AgentSmith.Infrastructure.Persistence.Postgresql")]
    public void EveryProvidersMigration_CreatesTheUniqueKeySlotIndex(string assembly)
    {
        var migration = File.ReadAllText(Directory.GetFiles(
            Path.Combine(ArchitectureSources.BackendRoot, assembly, "Migrations"), "*_AddModelRunSlots.cs").Single());

        migration.Should().MatchRegex(
            "name: \"IX_ModelRunSlots_Key_Slot\"[^;]*columns: new\\[\\] \\{ \"Key\", \"Slot\" \\}[^;]*unique: true",
            "the unique (Key, Slot) index is the guard on every provider");
    }

    private static async Task<IReadOnlyList<string>> AcquireAtOnceAsync(IModelRunSlots store, int runs, int limit)
    {
        using var start = new ManualResetEventSlim(false);
        var takers = Enumerable.Range(0, runs).Select(i => Task.Run(async () =>
        {
            start.Wait();
            return (Run: $"run-{i}", Admitted: await store.AcquireAsync(Key, limit, $"run-{i}", CancellationToken.None));
        })).ToList();
        start.Set();
        var results = await Task.WhenAll(takers);
        return [.. results.Where(r => r.Admitted).Select(r => r.Run)];
    }

    private async Task SeedRunAsync(
        string runId, string status = "running", bool finished = false, DateTimeOffset? startedAt = null,
        DateTimeOffset? heartbeatAt = null)
    {
        await using var db = new AgentSmithDbContext(SqliteOptions());
        db.Runs.Add(new Run
        {
            Id = runId, Project = "p", Pipeline = "code", TicketId = "1", Status = status,
            StartedAt = startedAt ?? DateTimeOffset.UtcNow, FinishedAt = finished ? DateTimeOffset.UtcNow : null,
            HeartbeatAt = heartbeatAt,
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedCapacityAsync(string runId, bool reserved)
    {
        await using var db = new AgentSmithDbContext(SqliteOptions());
        db.Add(new RunCapacity { RunId = runId, FootprintJson = "{}", TotalCpuNanos = 1, TotalMemBytes = 15L << 30, Reserved = reserved });
        await db.SaveChangesAsync();
    }

    private async Task SeedLeaseAsync(string ticket, string? runId, DateTimeOffset claimedAt)
    {
        await using var db = new AgentSmithDbContext(SqliteOptions());
        db.Add(new ActiveRun { Project = "p", TicketId = ticket, RunId = runId, ClaimedAt = claimedAt, HeartbeatAt = claimedAt });
        await db.SaveChangesAsync();
    }

    private async Task<List<string>> CapacityRunIdsAsync()
    {
        await using var db = new AgentSmithDbContext(SqliteOptions());
        return await db.Set<RunCapacity>().AsNoTracking().Select(c => c.RunId).ToListAsync();
    }

    private static async Task<List<ModelRunSlot>> RowsAsync(DbContextOptions<AgentSmithDbContext> options)
    {
        await using var db = new AgentSmithDbContext(options);
        return await db.Set<ModelRunSlot>().AsNoTracking().ToListAsync();
    }

    // The server's SQLite tuning (WAL, busy timeout), as RunRecordingExtensions installs it: concurrent writers wait.
    private DbContextOptions<AgentSmithDbContext> SqliteOptions() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite($"Data Source={_path}")
            .AddInterceptors(new SqliteTuningInterceptor(NullLogger<SqliteTuningInterceptor>.Instance)).Options;

    private static DbContextOptions<AgentSmithDbContext> ProviderOptions(PersistenceProvider provider, string connection)
    {
        var builder = new DbContextOptionsBuilder<AgentSmithDbContext>();
        builder.UseProvider(new PersistenceOptions { Provider = provider, ConnectionString = connection });
        return builder.Options;
    }

    private static DbModelRunSlots Store(
        DbContextOptions<AgentSmithDbContext> options, IUniqueViolationTranslator translator, ModelRunSlotSignal? signal = null,
        TimeProvider? clock = null)
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(options));
        services.AddSingleton(translator);
        return new DbModelRunSlots(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), signal ?? new ModelRunSlotSignal(),
            clock ?? TimeProvider.System);
    }
}
