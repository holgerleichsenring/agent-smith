using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;

namespace AgentSmith.Tests.Pocs;

/// <summary>
/// 2026-11-23-933da: the PoC list reads the rows of the scope's projects and leaves hidden ones out; a PoC is found by
/// id, its trail read in time order, and its project read for the route scope.
/// </summary>
public sealed class PocStoreTests : IDisposable
{
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly AgentSmithDbContext _db;
    private readonly CancellationToken _ct = CancellationToken.None;

    public PocStoreTests() => _db = MigratedStoreTemplate.Context(_connection);

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task ListAsync_ScopeOfProjects_ReturnsOnlyThoseProjectsRows()
    {
        await SeedAsync();

        var rows = await new PocRepository(_db).ListAsync(ProjectScope.Of(["poc-ann"]), _ct);

        rows.Select(p => p.Name).Should().Equal("todo");
    }

    [Fact]
    public async Task ListAsync_Everything_ReturnsAllNewestFirst_HiddenProjectLeftOut()
    {
        await SeedAsync();
        var repository = new PocRepository(_db);

        (await repository.ListAsync(ProjectScope.Everything, _ct)).Select(p => p.Name).Should().Equal("hidden", "shop", "todo");
        (await repository.ListAsync(ProjectScope.EverythingBut("poc-eve"), _ct)).Select(p => p.Name)
            .Should().Equal("shop", "todo");
    }

    [Fact]
    public async Task LatestRevisionsAsync_NamesEachPocsHighestRevision()
    {
        var todo = await PocRows.AddAsync(_db, PocRows.Poc("todo", "poc-ann"));
        var shop = await PocRows.AddAsync(_db, PocRows.Poc("shop", "poc-ann"));
        foreach (var n in new[] { 1, 2, 3 })
            _db.Add(new AgentSmith.Infrastructure.Persistence.Entities.PocRevision { PocId = todo.Id, Number = n, UploadSession = "s", UploadId = $"u{n}", State = "validated", Actor = "a" });
        await _db.SaveChangesAsync(_ct);

        var latest = await new PocRepository(_db).LatestRevisionsAsync([todo.Id, shop.Id], _ct);

        latest.Should().ContainSingle().Which.Should().Be(new KeyValuePair<long, int>(todo.Id, 3));
    }

    [Fact]
    public async Task FindAsync_KnownAndUnknownId_RowOrNull()
    {
        var poc = await PocRows.AddAsync(_db, PocRows.Poc("todo", "poc-ann"));
        var repository = new PocRepository(_db);

        (await repository.FindAsync(poc.Id, _ct))!.Project.Should().Be("poc-ann");
        (await repository.FindAsync(poc.Id + 100, _ct)).Should().BeNull();
    }

    [Fact]
    public async Task AuditListAsync_OnePoc_ItsEntriesOldestFirst()
    {
        var poc = await PocRows.AddAsync(_db, PocRows.Poc("todo", "poc-ann"));
        var other = await PocRows.AddAsync(_db, PocRows.Poc("shop", "poc-bob"));
        _db.AddRange(PocRows.Entry(poc.Id, PocActs.Deployed, "r-2", at: 20), PocRows.Entry(poc.Id, PocActs.Imported, "r-1", at: 10),
            PocRows.Entry(other.Id, PocActs.Imported, "r-3", at: 5));
        await _db.SaveChangesAsync();

        var trail = await new PocAuditRepository(_db).ListAsync(poc.Id, _ct);

        trail.Select(e => e.Act).Should().Equal(PocActs.Imported, PocActs.Deployed);
    }

    [Fact]
    public async Task PocAsync_KnownId_ReadsItsProject_UnknownIdIsNull()
    {
        var poc = await PocRows.AddAsync(_db, PocRows.Poc("todo", "poc-ann"));
        var reader = new ResourceProjectReader(_db);

        (await reader.PocAsync(poc.Id, _ct)).Should().Be("poc-ann");
        (await reader.PocAsync(poc.Id + 100, _ct)).Should().BeNull();
    }

    private async Task SeedAsync()
    {
        await PocRows.AddAsync(_db, PocRows.Poc("todo", "poc-ann"));
        await PocRows.AddAsync(_db, PocRows.Poc("shop", "poc-bob"));
        await PocRows.AddAsync(_db, PocRows.Poc("hidden", "poc-eve"));
    }
}
