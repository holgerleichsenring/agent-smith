using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-10-02-5ab2a: a migrated store with the server's state services over it, the way
/// AddServerState wires them. Each store a test builds with <see cref="Services"/> stands for one
/// replica; they all share the one database. 2026-10-02-5ab2d: <c>onDisk</c> gives every context
/// its own connection, for writers that really run at once.
/// </summary>
internal sealed class ServerStateStore : IDisposable
{
    private readonly SqliteConnection? _connection;
    private readonly string? _path;

    public ServerStateStore(
        TimeProvider? clock = null, Func<AgentSmithDbContext, IUnitOfWork>? unitOfWork = null, bool onDisk = false)
    {
        if (onDisk)
        {
            _path = Path.Combine(Path.GetTempPath(), $"agentsmith-state-{Guid.NewGuid():N}.db");
            MigratedStoreTemplate.CopyToFile(_path);
        }
        else _connection = MigratedStoreTemplate.OpenCopy();
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

    public AgentSmithDbContext Context() => _connection is not null
        ? MigratedStoreTemplate.Context(_connection)
        : new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite($"Data Source={_path}").Options);

    public void Dispose()
    {
        Services.Dispose();
        _connection?.Dispose();
        if (_path is null) return;
        SqliteConnection.ClearAllPools();
        foreach (var file in new[] { _path, _path + "-wal", _path + "-shm" })
            if (File.Exists(file)) File.Delete(file);
    }
}
