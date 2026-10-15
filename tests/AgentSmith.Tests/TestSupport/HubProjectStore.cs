using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Events;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-10-15-4a4dc: a migrated private store behind a scope factory, as the hub's run-project reads see
/// one — with every table read counted, so a case can say a path looked nothing up.
/// </summary>
internal sealed class HubProjectStore : IDisposable
{
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly ReadCount _reads = new();

    public HubProjectStore()
    {
        var options = new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;
        var services = new ServiceCollection();
        services.AddScoped(_ => new AgentSmithDbContext(options));
        services.AddScoped<IUnitOfWork>(sp => new CountingUnitOfWork(sp.GetRequiredService<AgentSmithDbContext>(), _reads));
        services.AddScoped<ResourceProjectReader>();
        Services = services.BuildServiceProvider();
    }

    public ServiceProvider Services { get; }

    /// <summary>How many times a table was opened for reading.</summary>
    public int Reads => _reads.Count;

    public RunProjectIndex Index() => new(Services.GetRequiredService<IServiceScopeFactory>());

    public async Task SeedAsync(Func<AgentSmithDbContext, Task> seed)
    {
        await using var scope = Services.CreateAsyncScope();
        await seed(scope.ServiceProvider.GetRequiredService<AgentSmithDbContext>());
    }

    public void Dispose()
    {
        Services.Dispose();
        _connection.Dispose();
    }

    private sealed class ReadCount
    {
        public int Count;
    }

    private sealed class CountingUnitOfWork(AgentSmithDbContext inner, ReadCount reads) : IUnitOfWork
    {
        public DbSet<TEntity> Set<TEntity>() where TEntity : class
        {
            Interlocked.Increment(ref reads.Count);
            return inner.Set<TEntity>();
        }

        public EntityEntry Add(object entity) => inner.Add(entity);
        public EntityEntry Update(object entity) => inner.Update(entity);
        public EntityEntry Remove(object entity) => inner.Remove(entity);
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => inner.SaveChangesAsync(cancellationToken);
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            inner.Database.BeginTransactionAsync(cancellationToken);
    }
}
