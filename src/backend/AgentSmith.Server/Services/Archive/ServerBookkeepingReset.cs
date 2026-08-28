using AgentSmith.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Server.Services.Archive;

/// <summary>
/// 2026-08-28-3793: clears the rows a running server writes ABOUT ITSELF, so an archive
/// can be written over them.
/// <para>
/// Tolerating those rows is not enough to make a restore work: the archive carries the same
/// four tables with keys of its own, and an insert onto an occupied key fails on the
/// constraint rather than merging. So the two things a server fills before anyone can press
/// the button — the callers it has observed, and the config store a boot migrated the
/// bootstrap role mapping into — are removed first. Both are replaced by what the archive
/// carries moments later, and the whole thing runs inside the import's transaction, so a
/// restore that fails anywhere leaves them exactly as they were.
/// </para>
/// </summary>
public sealed class ServerBookkeepingReset(ILogger<ServerBookkeepingReset> logger)
{
    public async Task ClearAsync(AgentSmithDbContext db, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        var callers = await db.ObservedCallers.ExecuteDeleteAsync(cancellationToken);
        // The reference edges point at the entities, so they go first.
        var refs = await db.ConfigRefs.ExecuteDeleteAsync(cancellationToken);
        var versions = await db.ConfigEntityVersions.ExecuteDeleteAsync(cancellationToken);
        var entities = await db.ConfigEntities.ExecuteDeleteAsync(cancellationToken);
        logger.LogInformation(
            "Cleared this server's own bookkeeping before a restore: {Callers} observed caller(s), "
            + "{Entities} config entity/entities, {Versions} version(s), {Refs} reference(s).",
            callers, entities, versions, refs);
    }
}
