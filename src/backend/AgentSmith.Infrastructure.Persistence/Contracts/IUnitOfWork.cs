using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;

namespace AgentSmith.Infrastructure.Persistence.Contracts;

/// <summary>
/// The DbContext IS the unit of work (the reference persistence pattern):
/// repositories depend on this abstraction, never on the concrete
/// AgentSmithDbContext and never on an IDbContextFactory. Registered SCOPED —
/// one unit of work per operation. Scoped is the web-request idiom; the
/// background singletons (projector, reaper, claim) are NOT requests, so they
/// open a scope per operation and resolve a scoped repository within it.
/// </summary>
public interface IUnitOfWork
{
    DbSet<TEntity> Set<TEntity>() where TEntity : class;
    EntityEntry Add(object entity);
    EntityEntry Update(object entity);
    EntityEntry Remove(object entity);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    // p0337: a multi-table delete (a run + its non-cascading satellites) must be
    // atomic — a partial delete would leave a held lease or a queue ghost.
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
