using AgentSmith.Application.Services.Claim;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Extensions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// p0246g: ONE persistence path. The server composition ALWAYS wires the
/// DB-backed lease + the scoped DbContext (IUnitOfWork) from config.persistence —
/// not a toggle. (The CLI never calls AddRelationalPersistence, so it stays
/// DB-free.)
/// </summary>
public sealed class RelationalPersistenceWiringTests
{
    [Fact]
    public void AddRelationalPersistence_AlwaysSwapsToDbBackedLease()
    {
        using var provider = BuildProvider();
        provider.GetRequiredService<IActiveRunLease>().Should().BeOfType<DbActiveRunLease>();
    }

    [Fact]
    public void AddRelationalPersistence_RegistersScopedUnitOfWorkFromConfig()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        // The DbContext IS the scoped unit of work, built from config.persistence
        // (sqlite default) — no IDbContextFactory.
        scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Should().BeOfType<AgentSmithDbContext>();
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        // config.persistence provides provider+connection (sqlite in-memory here).
        services.AddSingleton(new AgentSmithConfig
        {
            Persistence = new PersistenceConfig { Provider = "sqlite", ConnectionString = "Data Source=:memory:" },
        });
        // p0349: the DbContext connection is bootstrapped from the file (BootstrapConfig),
        // not AgentSmithConfig — the server loads its config from this very DB.
        services.AddSingleton(new BootstrapConfig(
            new PersistenceConfig { Provider = "sqlite", ConnectionString = "Data Source=:memory:" },
            new Dictionary<string, string>()));
        // Mirror the Server chain: the overrides register the NoOp default first,
        // then AddRelationalPersistence always RemoveAll-swaps it.
        services.AddSingleton<IActiveRunLease, NoOpActiveRunLease>();
        services.AddRelationalPersistence();
        return services.BuildServiceProvider();
    }
}
