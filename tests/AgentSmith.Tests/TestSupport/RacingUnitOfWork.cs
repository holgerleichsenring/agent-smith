using AgentSmith.Infrastructure.Persistence.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-10-02-5ab2a: a unit of work that lets another writer in just before its first save —
/// the window between "my update found no row" and "my insert lands" in which a concurrent
/// replica inserts the same key. The race runs before every save, so a test makes it fire once.
/// Deterministic, where two real threads would only sometimes race.
/// </summary>
internal sealed class RacingUnitOfWork(IUnitOfWork inner, Func<Task> race) : IUnitOfWork
{
    public DbSet<TEntity> Set<TEntity>() where TEntity : class => inner.Set<TEntity>();

    public EntityEntry Add(object entity) => inner.Add(entity);

    public EntityEntry Update(object entity) => inner.Update(entity);

    public EntityEntry Remove(object entity) => inner.Remove(entity);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await race();
        return await inner.SaveChangesAsync(cancellationToken);
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        inner.BeginTransactionAsync(cancellationToken);
}
