using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Extensions;

/// <summary>
/// Applies the configured provider to a DbContext options builder. The provider
/// is an explicit field (PersistenceOptions.Provider), so this is a plain switch
/// onto the matching Use{Sqlite|Npgsql|MySql} — no connection-string sniffing.
/// </summary>
public static class PersistenceOptionsExtensions
{
    // Pomelo requires a ServerVersion. At design time / without a live server we
    // pin a representative MySQL 8 version; runtime wiring (p0246c) can
    // AutoDetect against the real connection if desired.
    private static readonly ServerVersion MySqlVersion = new MySqlServerVersion(new Version(8, 0, 21));

    public static DbContextOptionsBuilder UseProvider(
        this DbContextOptionsBuilder builder, PersistenceOptions options) =>
        options.Provider switch
        {
            PersistenceProvider.Sqlite => builder.UseSqlite(options.ConnectionString),
            PersistenceProvider.Postgresql => builder.UseNpgsql(options.ConnectionString),
            PersistenceProvider.Mysql => builder.UseMySql(options.ConnectionString, MySqlVersion),
            _ => throw new ArgumentOutOfRangeException(
                nameof(options), options.Provider, "Unknown persistence provider."),
        };
}
