using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Server.Services.Archive;

/// <summary>
/// 2026-08-28-3793: the one thing a restore would actually collide with — an installation
/// that has already done work of its own.
/// <para>
/// The CLI asks whether any table holds a row. A server cannot: it upserts every
/// authenticated caller into the observed-caller table within half a minute, so the
/// operator's own sign-in guarantees a row, and a boot with roles in the bootstrap block
/// migrates a role mapping into the config store. A literal emptiness rule would refuse
/// every restore this endpoint will ever be offered. A recorded run is the thing that is
/// neither of those: it is work this installation did, and the archive would write over it.
/// </para>
/// </summary>
public sealed class NoRecordedRunCheck
{
    public async Task VerifyAsync(AgentSmithDbContext db, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        var recorded = await db.Runs.LongCountAsync(cancellationToken);
        if (recorded == 0) return;

        throw new DataArchiveException(
            $"This installation has already recorded {recorded} run(s). An archive is restored "
            + "into an installation that has run nothing, because a restore replaces every "
            + "table rather than merging into one. Nothing was written.");
    }
}
