using AgentSmith.Tests.TestSupport;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-10-06-03c7c: the pointer row is keyed by (project, ticket key) and caches the series' base
/// id — on a schema the migrations built, so the table the migration creates is the one read.
/// </summary>
public sealed class TicketSeriesRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AgentSmithDbContext _context;

    public TicketSeriesRepositoryTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _context = new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        MigratedStoreTemplate.CopyInto(_context);
    }

    private TicketSeriesSetRepository Sets => new(_context);

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task TicketSeries_SaveThenGet_KeepsTheSeriesAndUpsertsInPlace()
    {
        var repository = new TicketSeriesRepository(_context);
        var key = TicketKey.For("jira", "1").Value;

        await repository.SaveAsync("alpha", new SpecSetPointer(key, "api", "sha-1", 1), default);
        await repository.SaveAsync("alpha", new SpecSetPointer(key, "api", "sha-2", 2, SeriesId: "2026-10-06-0a0a"), default);

        var read = await repository.GetAsync("alpha", key, default);
        read!.SeriesId.Should().Be("2026-10-06-0a0a");
        read.RevisionSha.Should().Be("sha-2");
        (await _context.TicketSeries.CountAsync()).Should().Be(1, "one row per project and ticket key");
        (await repository.GetAsync("alpha", TicketKey.For("github", "1").Value, default))
            .Should().BeNull("the same number on another tracker is another ticket");
    }

    /// <summary>2026-10-12-3a36b: the heads a contradiction parked at survive the round trip as a
    /// JSON column, and a pointer saved without them clears the column.</summary>
    [Fact]
    public async Task TicketSeries_HandbackHeads_RoundTripAndClear()
    {
        var repository = new TicketSeriesRepository(_context);
        var key = TicketKey.For("azdo", "7").Value;
        var heads = new Dictionary<string, string> { ["Server"] = "abc123", ["Client"] = "def456" };

        await repository.SaveAsync("alpha", new SpecSetPointer(key, "Server", "sha-1", 1, HandbackHeads: heads), default);
        (await repository.GetAsync("alpha", key, default))!.HandbackHeads.Should().Equal(heads);

        await repository.SaveAsync("alpha", new SpecSetPointer(key, "Server", "sha-2", 2), default);
        (await repository.GetAsync("alpha", key, default))!.HandbackHeads.Should().BeNull();
    }

    [Fact]
    public async Task TicketSeries_RowWithoutShaMap_BackFillsTheCarryingRepository()
    {
        var repository = new TicketSeriesRepository(_context);
        var key = TicketKey.For("jira", "2").Value;
        await repository.SaveAsync("alpha", new SpecSetPointer(key, "api", "sha-1", 1), default);
        var row = await _context.TicketSeries.SingleAsync(t => t.TicketKey == key);
        row.RevisionShasJson = null;
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var read = await repository.GetAsync("alpha", key, default);

        read!.RevisionShas.Should().BeEquivalentTo(new Dictionary<string, string> { ["api"] = "sha-1" });
    }

    [Fact]
    public async Task TicketSeries_ShaMap_RoundTrips()
    {
        var repository = new TicketSeriesRepository(_context);
        var key = TicketKey.For("jira", "3").Value;
        var shas = new Dictionary<string, string> { ["api"] = "a1", ["web"] = "w1" };

        await repository.SaveAsync("alpha", new SpecSetPointer(key, "api", "a1", 1, RevisionShas: shas), default);

        (await repository.GetAsync("alpha", key, default))!.RevisionShas.Should().BeEquivalentTo(shas);
    }

    /// <summary>2026-10-12-f5f7d: a row written before executed ids reads its count as that many
    /// leading specs of the set it is read against; a row with ids keeps them.</summary>
    [Fact]
    public async Task TicketSeries_LegacyExecutedThrough_BackfillsIds()
    {
        var repository = new TicketSeriesRepository(_context);
        var legacy = TicketKey.For("jira", "4").Value;
        var current = TicketKey.For("jira", "5").Value;
        await repository.SaveAsync("alpha", new SpecSetPointer(legacy, "api", "a1", 1, ExecutedThrough: 1), default);
        await repository.SaveAsync("alpha", new SpecSetPointer(current, "api", "a1", 1, ExecutedThrough: 1,
            ExecutedIds: ["2026-10-12-0a0ab"]), default);
        _context.ChangeTracker.Clear();
        IReadOnlyList<DerivedSpec> stored =
        [
            new(new Contracts.Models.SpecDraft("2026-10-12-0a0aa", "g", "spec: x", []), "a", string.Empty, []),
            new(new Contracts.Models.SpecDraft("2026-10-12-0a0ab", "g", "spec: y", []), "b", string.Empty, []),
        ];

        var old = (await repository.GetAsync("alpha", legacy, default))!;
        var kept = (await repository.GetAsync("alpha", current, default))!;

        old.ExecutedIds.Should().BeNull();
        old.ExecutedIdsOf(stored).Should().Equal("2026-10-12-0a0aa");
        kept.ExecutedIdsOf(stored).Should().Equal("2026-10-12-0a0ab");
    }

    /// <summary>2026-11-28-db88a: a reopen or a hand-back counter reads the pointer and saves it back; the set a run stored
    /// meanwhile stays.</summary>
    [Fact]
    public async Task SaveAsync_APointerSave_NeverTouchesTheStoredSet()
    {
        var repository = new TicketSeriesRepository(_context);
        var key = TicketKey.For("jira", "6").Value;
        await repository.SaveAsync("alpha", new SpecSetPointer(key, "api", "sha-1", 1), default);
        _context.ChangeTracker.Clear();
        var read = (await repository.GetAsync("alpha", key, default))!;
        await Sets.SaveSetAsync("alpha", Set(key, "Revision two"), default);
        _context.ChangeTracker.Clear();

        await repository.SaveAsync("alpha", read with { RepeatedHandbackCount = 3 }, default);
        _context.ChangeTracker.Clear();

        (await Sets.GetSetAsync("alpha", key, default))!.Goal.Should().Be("Revision two");
        (await repository.GetAsync("alpha", key, default))!.RepeatedHandbackCount.Should().Be(3);
    }

    /// <summary>2026-11-28-db88a: the one-time import of a ticket in flight stores only into a row with no set.</summary>
    [Fact]
    public async Task SaveSetAsync_OnlyIfAbsent_KeepsANewerSet()
    {
        var repository = new TicketSeriesRepository(_context);
        var key = TicketKey.For("jira", "7").Value;
        await repository.SaveAsync("alpha", new SpecSetPointer(key, "api", "sha-1", 1), default);
        _context.ChangeTracker.Clear();
        (await Sets.GetSetAsync("alpha", key, default)).Should().BeNull("a row from before sets were stored");

        await Sets.SaveSetAsync("alpha", Set(key, "Imported"), default, onlyIfAbsent: true);
        _context.ChangeTracker.Clear();
        await Sets.SaveSetAsync("alpha", Set(key, "Imported again"), default, onlyIfAbsent: true);
        _context.ChangeTracker.Clear();
        (await Sets.GetSetAsync("alpha", key, default))!.Goal.Should().Be("Imported");

        await Sets.SaveSetAsync("alpha", Set(key, "Published"), default);
        _context.ChangeTracker.Clear();
        (await Sets.GetSetAsync("alpha", key, default))!.Goal.Should().Be("Published");
    }

    [Fact]
    public async Task SaveSetAsync_NoRow_CreatesTheRowWithTheSet()
    {
        var repository = new TicketSeriesRepository(_context);
        var key = TicketKey.For("jira", "8").Value;

        await Sets.SaveSetAsync("alpha", Set(key, "First"), default, onlyIfAbsent: true);
        _context.ChangeTracker.Clear();

        (await Sets.GetSetAsync("alpha", key, default))!.Goal.Should().Be("First");
        (await repository.GetAsync("alpha", key, default))!.SeriesId.Should().Be("2026-11-28-0a0a");
        (await _context.TicketSeries.CountAsync(t => t.TicketKey == key)).Should().Be(1);
    }

    [Fact]
    public async Task GetSetAsync_AStoredValueThatDoesNotReadBack_Throws()
    {
        var repository = new TicketSeriesRepository(_context);
        var key = TicketKey.For("jira", "9").Value;
        await Sets.SaveSetAsync("alpha", Set(key, "First"), default);
        var row = await _context.TicketSeries.SingleAsync(t => t.TicketKey == key);
        row.SetJson = "{\"key\":";
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        await FluentActions.Awaiting(() => Sets.GetSetAsync("alpha", key, default))
            .Should().ThrowAsync<InvalidDataException>("something present and unreadable is never read as nothing");
    }

    private static SpecSet Set(string key, string goal) =>
        new(key, [new DerivedSpec(new Contracts.Models.SpecDraft("2026-11-28-0a0aa", "g", "spec: x", []), "a", string.Empty, [])],
            SpecAccounting.Empty, [new SpecRevision(1, "initial", DateTimeOffset.UnixEpoch)], SpecSource.Approved,
            Series: "2026-11-28-0a0a") { Goal = goal };
}
