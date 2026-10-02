using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Server.Services.Archive;

/// <summary>
/// 2026-08-28-3793: clears the rows a running server writes ABOUT ITSELF, so an archive
/// can be written over them.
/// <para>
/// Tolerating those rows is not enough to make a restore work: the archive carries the same
/// four tables with keys of its own, and an insert onto an occupied key fails on the
/// constraint rather than merging. So what a server fills before anyone can press the button —
/// the callers it has observed, its connection discoveries (2026-10-02-5ab2a), its pending chat
/// confirmations (2026-10-02-5ab2d), and the config store a boot migrated the bootstrap role
/// mapping into — is removed first. All of it is replaced by what the archive carries moments
/// later, and the whole thing runs inside the import's transaction, so a
/// restore that fails anywhere leaves them exactly as they were.
/// </para>
/// </summary>
public sealed class ServerBookkeepingReset(ILogger<ServerBookkeepingReset> logger)
{
    public async Task ClearAsync(AgentSmithDbContext db, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        var callers = await db.ObservedCallers.ExecuteDeleteAsync(cancellationToken);
        // 2026-10-02-5ab2a: rebuilt by the next discovery, and keyed like the archive's copy.
        var discoveries = await db.Set<ConnectionDiscovery>().ExecuteDeleteAsync(cancellationToken);
        // 2026-10-02-5ab2d: a pending chat confirmation belongs to this server's channels.
        var clarifications = await db.Set<PendingClarification>().ExecuteDeleteAsync(cancellationToken);
        // The reference edges point at the entities, so they go first.
        var refs = await db.ConfigRefs.ExecuteDeleteAsync(cancellationToken);
        var versions = await db.ConfigEntityVersions.ExecuteDeleteAsync(cancellationToken);
        var entities = await db.ConfigEntities.ExecuteDeleteAsync(cancellationToken);
        logger.LogInformation(
            "Cleared this server's own bookkeeping before a restore: {Callers} observed caller(s), "
            + "{Discoveries} connection discovery row(s), {Clarifications} pending clarification(s), "
            + "{Entities} config entity/entities, {Versions} version(s), {Refs} reference(s).",
            callers, discoveries, clarifications, entities, versions, refs);
    }
}
