using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AgentSmith.Server.Services.Archive;

/// <summary>
/// 2026-08-28-3793: what a SERVER demands of its own database before an archive is written
/// into it — the policy that replaces the CLI's literal emptiness rule.
/// <para>
/// It refuses on the one collision that matters, a run this installation has already
/// recorded, and it removes what the server wrote about itself so the archive's own copy of
/// those tables can land. Both halves run inside the import's transaction: a refusal writes
/// nothing, and a failure after the clearing puts the cleared rows back.
/// </para>
/// </summary>
public sealed class ServerRestorePolicy(NoRecordedRunCheck runs, ServerBookkeepingReset reset)
    : IImportTargetPolicy
{
    public async Task EnforceAsync(
        AgentSmithDbContext db, IReadOnlyList<IEntityType> types, CancellationToken cancellationToken = default)
    {
        await runs.VerifyAsync(db, cancellationToken);
        await reset.ClearAsync(db, cancellationToken);
    }
}
