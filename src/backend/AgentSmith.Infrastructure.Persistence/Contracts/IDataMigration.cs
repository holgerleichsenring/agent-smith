namespace AgentSmith.Infrastructure.Persistence.Contracts;

/// <summary>
/// 2026-11-22-747da: a change to stored DATA, applied like a schema migration — once per store by `database migrate`,
/// recorded in DataMigrationHistory, never re-checked. Every one is idempotent, because two replicas' init containers
/// may run the same one at once.
/// </summary>
public interface IDataMigration
{
    /// <summary>The id it is recorded under; never renamed.</summary>
    string Id { get; }

    /// <summary>
    /// The 14-digit timestamp of the schema migration it shipped with, or null. A store whose applied schema
    /// migrations, read before this migrate, include one at or after it has already run it on an earlier migrate:
    /// it is recorded without running.
    /// </summary>
    string? ShippedWith { get; }

    /// <summary>Applies it; true when it is complete and is recorded, false when the next migrate tries again.</summary>
    Task<bool> RunAsync(AgentSmithDbContext db, CancellationToken ct);
}
