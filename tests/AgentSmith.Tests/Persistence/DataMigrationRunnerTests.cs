using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Persistence.Services.DataMigrations;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-11-22-747da: a data migration is applied like a schema migration — once per store, recorded, never looked at
/// again. Proven on a migrated SQLite store, the way `database migrate` meets an installation's database.
/// </summary>
[Collection(RelationalStoreCollection.Name)]
public sealed class DataMigrationRunnerTests : IDisposable
{
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly RecordingLoggerProvider _logs = new();

    [Fact]
    public async Task Run_UnrecordedMigration_RunsOnceAndIsRecorded()
    {
        var counted = new CountingMigration("test-once");

        await RunAsync(counted);
        await RunAsync(counted);

        counted.Runs.Should().Be(1, "a recorded migration is never run again");
        (await RecordsAsync()).Should().ContainSingle(r => r.Id == "test-once" && !r.Proven);
    }

    [Fact]
    public async Task Run_WatermarkReachedBeforeThisMigrate_RecordsWithoutRunning()
    {
        var shipped = new CountingMigration("test-shipped", shippedWith: "20261012181922");

        await RunAsync(shipped, appliedBefore: ["20261012181922_AddRunSpecIdColumns"]);

        shipped.Runs.Should().Be(0, "every earlier migrate on this store already ran it");
        (await RecordsAsync()).Should().ContainSingle(r => r.Id == "test-shipped" && r.Proven);
    }

    [Fact]
    public async Task Run_WatermarkNotReached_Runs()
    {
        var shipped = new CountingMigration("test-shipped", shippedWith: "20261012181922");

        await RunAsync(shipped, appliedBefore: ["20260825085711_AddRunRecordIdentity"]);

        shipped.Runs.Should().Be(1);
    }

    [Fact]
    public async Task Run_MigrationNotComplete_IsNotRecordedAndRunsAgain()
    {
        var failing = new CountingMigration("test-incomplete", completes: false);

        await RunAsync(failing);
        await RunAsync(failing);

        failing.Runs.Should().Be(2);
        (await RecordsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Run_AnotherMigrateRecordedItMeanwhile_CountsAsApplied()
    {
        var raced = new CountingMigration("test-raced", recordsItself: true);

        var act = () => RunAsync(raced);

        await act.Should().NotThrowAsync("the other replica's row says what this one would");
        (await RecordsAsync()).Should().ContainSingle(r => r.Id == "test-raced");
    }

    [Fact]
    public async Task Migrate_MigratedStore_RunsTheStoreMigrationsOnceAndRecordsThem()
    {
        var more = new CountingMigration("test-more");

        await using (var db = Context()) await Migrator().MigrateAsync(db, [more], CancellationToken.None);
        await using (var db = Context()) await Migrator().MigrateAsync(db, [more], CancellationToken.None);

        more.Runs.Should().Be(1);
        var records = await RecordsAsync();
        records.Should().Contain(r => r.Id == "2026-10-12-967ab-spec-names" && r.Proven,
            "a store at AddRunSpecIdColumns had the rewrite on every earlier migrate");
        records.Should().Contain(r => r.Id == "2026-11-22-a720a-kept-set-carry" && !r.Proven);
        _logs.Entries.Should().NotContain(e => e.Message.StartsWith("Run store repair", StringComparison.Ordinal),
            "the unique trail index exists, so no duplicate can");
    }

    [Fact]
    public async Task Migrate_StoreThisMigrateCreates_RunsAndRecordsNoDataMigration()
    {
        await using var empty = new SqliteConnection("Data Source=:memory:");
        await empty.OpenAsync();
        var more = new CountingMigration("test-fresh");
        await using var db = new AgentSmithDbContext(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(empty).Options);

        await Migrator().MigrateAsync(db, [more], CancellationToken.None);

        more.Runs.Should().Be(0, "a store with no rows has nothing to change");
        (await db.Set<DataMigrationRecord>().CountAsync()).Should().Be(0,
            "an empty ledger keeps the store an empty archive-import target");
    }

    public void Dispose()
    {
        _connection.Dispose();
        _logs.Dispose();
    }

    private async Task RunAsync(IDataMigration migration, IReadOnlyCollection<string>? appliedBefore = null)
    {
        await using var db = Context();
        await Services().GetRequiredService<DataMigrationRunner>()
            .RunAsync(db, appliedBefore ?? ["20260101000000_Initial"], [migration], CancellationToken.None);
    }

    private async Task<List<DataMigrationRecord>> RecordsAsync()
    {
        await using var db = Context();
        return await db.Set<DataMigrationRecord>().AsNoTracking().ToListAsync();
    }

    private RunStoreMigrator Migrator() => Services().GetRequiredService<RunStoreMigrator>();

    private ServiceProvider Services() =>
        new ServiceCollection().AddLogging(b => b.AddProvider(_logs)).AddRunStoreRepair().BuildServiceProvider();

    private AgentSmithDbContext Context() =>
        new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);

    private sealed class CountingMigration(
        string id, string? shippedWith = null, bool completes = true, bool recordsItself = false) : IDataMigration
    {
        public int Runs { get; private set; }

        public string Id => id;

        public string? ShippedWith => shippedWith;

        public async Task<bool> RunAsync(AgentSmithDbContext db, CancellationToken ct)
        {
            Runs++;
            if (!recordsItself) return completes;
            db.Set<DataMigrationRecord>().Add(new DataMigrationRecord { Id = id, AppliedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
            return completes;
        }
    }
}
