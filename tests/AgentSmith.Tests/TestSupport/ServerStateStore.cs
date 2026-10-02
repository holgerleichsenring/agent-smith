using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-10-02-5ab2a: a migrated store with the server's state services over it, the way
/// AddServerState wires them. Each store a test builds with <see cref="Services"/> stands for one
/// replica; they all share the one database.
/// </summary>
internal sealed class ServerStateStore : IDisposable
{
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();

    public ServerStateStore(TimeProvider? clock = null, Func<AgentSmithDbContext, IUnitOfWork>? unitOfWork = null)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => (unitOfWork ?? (db => db))(Context()));
        services.AddSingleton<IUniqueViolationTranslator, SqliteUniqueViolationTranslator>();
        services.AddSingleton(clock ?? TimeProvider.System);
        services.AddLogging();
        services.AddServerState();
        Services = services.BuildServiceProvider();
    }

    public ServiceProvider Services { get; }

    public IServiceScopeFactory ScopeFactory => Services.GetRequiredService<IServiceScopeFactory>();

    public AgentSmithDbContext Context() => MigratedStoreTemplate.Context(_connection);

    public void Dispose()
    {
        Services.Dispose();
        _connection.Dispose();
    }
}
