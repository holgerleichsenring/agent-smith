namespace AgentSmith.Infrastructure.Persistence.Contracts;

/// <summary>
/// The DbContext IS the unit of work: callers stage changes on the tracked
/// entities and commit them with one SaveChangesAsync. Kept as an interface so
/// handlers depend on the abstraction, not the concrete AgentSmithDbContext.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
