using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-10-02-5f89c: one store serves the hot read and the durable record alike; the CLI keeps
/// memory and disk. 2026-10-02-5ab2a: on the server that store is the database row — resolving it
/// reaches neither the database nor Redis.
/// </summary>
[Trait("TestProcess", "env-1")]
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class ConnectionDiscoveryCompositionTests
{
    [Fact]
    public void ConnectionDiscoveryComposition_Server_ResolvesTheDatabaseStore()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        ServerCompositionBuilder.ConfigureServices(services, "agentsmith.yml");

        services.Where(d => d.ServiceType == typeof(IConnectionRepoSnapshot)).Should().ContainSingle();
        services.Where(d => d.ServiceType == typeof(IConnectionRepoSnapshotStore)).Should().ContainSingle();
        using var provider = services.BuildServiceProvider();
        var snapshot = provider.GetRequiredService<IConnectionRepoSnapshot>();
        snapshot.Should().BeOfType<DbConnectionRepoSnapshot>();
        provider.GetRequiredService<IConnectionRepoSnapshotStore>().Should().BeSameAs(snapshot);
    }

    [Fact]
    public void CoreComposition_WithoutTheServer_KeepsMemoryAndDisk()
    {
        var services = new ServiceCollection().AddAgentSmithCore();

        services.Single(d => d.ServiceType == typeof(IConnectionRepoSnapshot)).ImplementationType
            .Should().Be(typeof(InMemoryConnectionRepoSnapshot));
        services.Single(d => d.ServiceType == typeof(IConnectionRepoSnapshotStore)).ImplementationType
            .Should().Be(typeof(DiskConnectionRepoSnapshotStore));
    }
}
