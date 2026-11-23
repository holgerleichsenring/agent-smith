using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Interceptors;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Migration.Services;

/// <summary>
/// 2026-11-22-747da: the context `database migrate` runs on, built by hand from the bootstrap persistence block (moved
/// from the CLI's DatabaseCommand unchanged).
/// </summary>
public sealed class MigrateContextFactory
{
    public async Task<AgentSmithDbContext> OpenAsync(PersistenceOptions options, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(options);
        var builder = new DbContextOptionsBuilder<AgentSmithDbContext>();
        builder.UseProvider(options);
        // The shared migration set's model snapshot is generated under SQLite (the design-time default). Under another
        // provider that runs it the runtime model gains provider annotations the snapshot lacks, which EF 9 misreads as
        // "pending model changes" — suppress the warning there. PostgreSQL's set is generated against the current model
        // (2026-10-28-ebb0), so like SQLite it keeps the warning as a real forgotten-migration guard.
        if (options.Provider is not (PersistenceProvider.Sqlite or PersistenceProvider.Postgresql))
            builder.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        var db = new AgentSmithDbContext(builder.Options);
        if (options.Provider == PersistenceProvider.Sqlite) await TuneSqliteAsync(db, ct);
        return db;
    }

    // p0348: leave the SQLite file in WAL before the server ever opens it. Without this the migrate one-shot leaves the
    // file in DELETE journal mode and the first server connection must perform the WAL conversion under boot
    // contention — the intermittent "database is locked" at startup. Runs on the context's own connection so the mode
    // is set before MigrateAsync opens its transactions.
    private static async Task TuneSqliteAsync(AgentSmithDbContext db, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync(ct);
        await using var pragma = connection.CreateCommand();
        pragma.CommandText = SqliteTuningInterceptor.TunePragmas;
        await pragma.ExecuteNonQueryAsync(ct);
    }
}
