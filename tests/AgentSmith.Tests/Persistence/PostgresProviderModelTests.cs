using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Interceptors;
using AgentSmith.Infrastructure.Persistence.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-10-28-ebb0: the two PostgreSQL-only adjustments, checked offline — the UTC conversion
/// is on every timestamp column there and on none elsewhere, and the NUL scrub rewrites a
/// command's string parameters and leaves everything else alone.
/// </summary>
public sealed class PostgresProviderModelTests
{
    [Fact]
    public void UtcTimestampConfiguration_OnPostgres_ConvertsEveryTimestampColumn()
    {
        using var db = Context(PersistenceProvider.Postgresql, "Host=localhost;Database=x");

        var timestamps = Timestamps(db).ToList();

        timestamps.Should().NotBeEmpty();
        timestamps.Should().OnlyContain(p => p.GetValueConverter() != null);
        var converter = timestamps[0].GetValueConverter()!;
        ((DateTimeOffset)converter.ConvertToProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.FromHours(2)))!)
            .Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void UtcTimestampConfiguration_OnSqlite_LeavesTheOffsetAlone()
    {
        using var db = Context(PersistenceProvider.Sqlite, "Data Source=:memory:");

        Timestamps(db).Should().OnlyContain(p => p.GetValueConverter() == null);
    }

    [Fact]
    public void UseProvider_Postgres_RegistersTheNulScrub()
    {
        using var db = Context(PersistenceProvider.Postgresql, "Host=localhost;Database=x");

        db.GetService<IDbContextOptions>().FindExtension<CoreOptionsExtension>()!.Interceptors
            .Should().Contain(PostgresNulTextInterceptor.Instance);
    }

    [Fact]
    public void Scrub_StringParameters_ReplacesEveryNulAndKeepsTheRest()
    {
        using var command = new NpgsqlCommand();
        command.Parameters.AddWithValue("a", "x\0y\0");
        command.Parameters.AddWithValue("b", "clean");
        command.Parameters.AddWithValue("c", 42);

        PostgresNulTextInterceptor.Scrub(command);

        command.Parameters["a"].Value.Should().Be("x�y�");
        command.Parameters["b"].Value.Should().Be("clean");
        command.Parameters["c"].Value.Should().Be(42);
    }

    private static IEnumerable<Microsoft.EntityFrameworkCore.Metadata.IProperty> Timestamps(AgentSmithDbContext db) =>
        db.Model.GetEntityTypes().SelectMany(t => t.GetProperties())
            .Where(p => p.ClrType == typeof(DateTimeOffset) || p.ClrType == typeof(DateTimeOffset?));

    private static AgentSmithDbContext Context(PersistenceProvider provider, string connection)
    {
        var builder = new DbContextOptionsBuilder<AgentSmithDbContext>();
        builder.UseProvider(new PersistenceOptions { Provider = provider, ConnectionString = connection });
        return new AgentSmithDbContext(builder.Options);
    }
}
