using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-10-28-ebb0: a PostgreSQL database of this test run's own, migrated by the shipped
/// PostgreSQL migration set and dropped afterwards by the caller. Unlike
/// <see cref="ScratchSqlServer"/> it does not silence the pending-model-changes warning: the
/// PostgreSQL set is generated against the current model, so a pending change is a defect.
/// </summary>
internal static class ScratchPostgres
{
    /// <summary>A migrated database named for <paramref name="purpose"/> and this run.</summary>
    public static async Task<AgentSmithDbContext> MigratedAsync(string connectionString, string purpose)
    {
        var db = new AgentSmithDbContext(Options(Named(connectionString, purpose)));
        await db.Database.MigrateAsync();
        return db;
    }

    /// <summary>Options onto an existing database, for a second context over the same store.</summary>
    public static DbContextOptions<AgentSmithDbContext> Options(string connectionString)
    {
        var builder = new DbContextOptionsBuilder<AgentSmithDbContext>();
        builder.UseProvider(new PersistenceOptions
        {
            Provider = PersistenceProvider.Postgresql,
            ConnectionString = connectionString,
        });
        return builder.Options;
    }

    private static string Named(string connectionString, string purpose) =>
        new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = $"agentsmith_{purpose}_{Guid.NewGuid():N}"[..40],
        }.ConnectionString;
}
