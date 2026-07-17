using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Extensions;

/// <summary>
/// Applies the configured provider to a DbContext options builder. The provider
/// is an explicit field (PersistenceOptions.Provider), so this is a plain switch
/// onto the matching Use{Sqlite|Npgsql|MySql|SqlServer} — no connection-string
/// sniffing.
/// </summary>
public static class PersistenceOptionsExtensions
{
    // Pomelo requires a ServerVersion. At design time / without a live server we
    // pin a representative MySQL 8 version; runtime wiring (p0246c) can
    // AutoDetect against the real connection if desired.
    private static readonly ServerVersion MySqlVersion = new MySqlServerVersion(new Version(8, 0, 21));

    // SQL Server can't reuse the shared migration set (SQLite column types baked
    // into every operation), so its migrations live in a dedicated assembly.
    // Named by string — the Persistence assembly can't reference that project
    // (it references back for the DbContext), so there's no compile-time handle.
    private const string SqlServerMigrationsAssembly = "AgentSmith.Infrastructure.Persistence.SqlServer";

    public static DbContextOptionsBuilder UseProvider(
        this DbContextOptionsBuilder builder, PersistenceOptions options) =>
        options.Provider switch
        {
            PersistenceProvider.Sqlite => builder.UseSqlite(options.ConnectionString),
            PersistenceProvider.Postgresql => builder.UseNpgsql(options.ConnectionString),
            PersistenceProvider.Mysql => builder.UseMySql(options.ConnectionString, MySqlVersion),
            PersistenceProvider.SqlServer => builder.UseSqlServer(
                options.ConnectionString, o => o.MigrationsAssembly(SqlServerMigrationsAssembly)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(options), options.Provider, "Unknown persistence provider."),
        };
}
