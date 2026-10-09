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
}
