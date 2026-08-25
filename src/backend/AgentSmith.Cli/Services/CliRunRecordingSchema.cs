using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Cli.Services;

/// <summary>
/// p0423: brings the CLI's own run store up to date before a run starts writing to it.
/// <para>
/// The SERVER never migrates on startup — replicas would race and operators would be
/// surprised, so <c>agentsmith database migrate</c> is its single deliberate entry point.
/// A CLI one-shot has neither replicas nor an operator standing by: it owns the file it
/// writes, and demanding a separate migrate step before a local run would reproduce
/// exactly the "nothing was recorded" state this phase exists to end. So the local SQLite
/// store migrates itself; any shared provider is assumed current, as on the server.
/// </para>
/// <para>Failure to prepare the store never fails the run — it is announced and the run
/// goes on unrecorded.</para>
/// </summary>
public sealed class CliRunRecordingSchema(
    AgentSmithDbContext db, RunStoreMigrator migrator, ILogger<CliRunRecordingSchema> logger)
{
    public async Task<bool> EnsureAsync(bool isLocalSqlite, CancellationToken cancellationToken)
    {
        if (!isLocalSqlite) return true;
        try
        {
            // 2026-08-25-61f1: through the migrator, so the local store gets the same
            // repair-then-constrain order the deployment entry point gets.
            await migrator.MigrateAsync(db, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not prepare the local run store — this run stays unrecorded.");
            return false;
        }
    }
}
