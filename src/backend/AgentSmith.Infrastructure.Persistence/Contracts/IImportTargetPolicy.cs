using Microsoft.EntityFrameworkCore.Metadata;

namespace AgentSmith.Infrastructure.Persistence.Contracts;

/// <summary>
/// 2026-08-28-3793: what an installation demands of a database before an archive is
/// written into it, and the one thing about an import that is not the same everywhere.
/// <para>
/// The CLI writes into a database whose server is not running — nothing but a migration
/// has touched it — so it demands literal emptiness. A server writes into its OWN
/// database while answering the request that asks for it, and by then it has written
/// rows about itself: every authenticated caller lands in the observed-caller table
/// within half a minute, and a boot with roles in the bootstrap block migrates a role
/// mapping into the config store. A literal emptiness rule would refuse every restore
/// the server endpoint will ever be offered, so the server states its own policy.
/// </para>
/// <para>
/// It runs INSIDE the import's transaction: a policy that has to clear the rows it
/// tolerates must be undone with the import if anything after it fails.
/// </para>
/// </summary>
public interface IImportTargetPolicy
{
    /// <summary>
    /// Leaves <paramref name="db"/> fit to receive an archive, or throws
    /// <see cref="Domain.Exceptions.DataArchiveException"/> naming the rule that refuses it.
    /// </summary>
    Task EnforceAsync(
        AgentSmithDbContext db, IReadOnlyList<IEntityType> types, CancellationToken cancellationToken = default);
}
