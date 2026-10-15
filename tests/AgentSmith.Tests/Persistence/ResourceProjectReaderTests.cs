using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;

namespace AgentSmith.Tests.Persistence;

/// <summary>2026-10-15-4a4db: the project column of a run, a conversation and an image — null for no row.</summary>
public sealed class ResourceProjectReaderTests : IDisposable
{
    private const string Dashboard = "dashboard";
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly AgentSmithDbContext _db;
    private readonly ResourceProjectReader _reader;

    public ResourceProjectReaderTests()
    {
        _db = MigratedStoreTemplate.Context(_connection);
        _reader = new ResourceProjectReader(_db);
    }

    [Fact]
    public async Task ResourceProjectReader_RunSessionDialogAndImage_ReadTheProjectOrNull()
    {
        await ProjectScopeRows.RunAsync(_db, "r-1", "alpha");
        await ProjectScopeRows.SessionAsync(_db, "s-beta", "d-beta", "beta");
        await ProjectScopeRows.SessionAsync(_db, "s-fresh", "d-fresh", "");
        await ProjectScopeRows.ImageAsync(_db, 7, "s-beta");
        await ProjectScopeRows.LegacyImageAsync(_db, 8, "s-beta");
        var ct = CancellationToken.None;

        (await _reader.RunAsync("r-1", ct)).Should().Be("alpha");
        (await _reader.RunAsync("r-none", ct)).Should().BeNull();
        (await _reader.SessionAsync(Dashboard, "s-beta", ct)).Should().Be("beta");
        (await _reader.SessionAsync("slack", "s-beta", ct)).Should().BeNull("a session lives on one platform");
        (await _reader.SessionAsync(Dashboard, "s-fresh", ct)).Should().Be("", "grounded on no project yet");
        (await _reader.OpenDialogAsync(Dashboard, "d-beta", ct)).Should().Be("beta");
        (await _reader.OpenDialogAsync(Dashboard, "d-none", ct)).Should().BeNull();
        (await _reader.ImageAsync(Dashboard, 7, ct)).Should().Be("beta");
        (await _reader.ImageAsync(Dashboard, 8, ct)).Should().Be("beta", "a legacy attachment names its session too");
        (await _reader.ImageAsync(Dashboard, 9, ct)).Should().BeNull();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
