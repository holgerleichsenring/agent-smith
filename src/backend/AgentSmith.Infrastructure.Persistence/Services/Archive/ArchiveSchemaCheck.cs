using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services.Archive;

/// <summary>
/// 2026-08-28-2af6: refuses an archive taken at a schema the target does not share, before
/// anything is written, rather than discovering it halfway through.
/// <para>
/// The comparison is on the migration's NAME. The two providers keep separate migration
/// assemblies with disjoint histories and different timestamp prefixes, so comparing
/// recorded ids would refuse every SQLite-to-SQL-Server import — the one journey the
/// archive exists for.
/// </para>
/// </summary>
public sealed class ArchiveSchemaCheck(MigrationHeadName head)
{
    public async Task VerifyAsync(
        AgentSmithDbContext db, DataArchiveManifest manifest, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(manifest);
        var target = head.Of(await db.Database.GetAppliedMigrationsAsync(ct));
        if (string.Equals(target, manifest.SchemaHead, StringComparison.Ordinal)) return;

        throw new DataArchiveException(
            $"The archive was taken at schema '{manifest.SchemaHead}' and this database is at "
            + $"'{target}'. Bring the target to the same schema — `agentsmith database migrate` "
            + "— and import again. Nothing was written.");
    }
}
