using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// A SQL Server database of this test run's own, migrated by the shipped SQL Server
/// migration set and dropped afterwards.
/// <para>
/// It exists so AGENTSMITH_TEST_DB_CONNSTR means ONE thing across the suite: a server that
/// can be reached. Two tests reading it as "an already-migrated database" and as "a server
/// to create one on" is a trap for whoever sets it — they get failures about missing tables
/// and have no way to tell them from a real defect.
/// </para>
/// </summary>
internal static class ScratchSqlServer
{
    /// <summary>A migrated database named for <paramref name="purpose"/> and this run.</summary>
    public static async Task<AgentSmithDbContext> MigratedAsync(string connectionString, string purpose)
    {
        var connection = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = $"agentsmith_{purpose}_{Guid.NewGuid():N}"[..32],
        };
        var builder = new DbContextOptionsBuilder<AgentSmithDbContext>();
        builder.UseProvider(new PersistenceOptions
        {
            Provider = PersistenceProvider.SqlServer,
            ConnectionString = connection.ConnectionString,
        });
        builder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        var db = new AgentSmithDbContext(builder.Options);
        await db.Database.MigrateAsync();
        return db;
    }
}
