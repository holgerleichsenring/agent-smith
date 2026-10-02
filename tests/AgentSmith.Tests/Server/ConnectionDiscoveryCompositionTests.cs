using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Services.Persistence;
using AgentSmith.Server.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-10-02-5f89c: the server keeps connection discovery in one Redis hash per connection for
/// the hot read and the durable store alike; the CLI keeps memory and disk. Read off the
/// descriptors, so no Redis is reached.
/// </summary>
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class ConnectionDiscoveryCompositionTests
{
    [Fact]
    public void ServerComposition_DiscoveryStores_AreTheOneRedisHashStore()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        ServerCompositionBuilder.ConfigureServices(services, "agentsmith.yml");

        services.Where(d => d.ServiceType == typeof(IConnectionRepoSnapshot)).Should().ContainSingle()
            .Which.ImplementationType.Should().BeNull("it forwards to the one RedisConnectionRepoSnapshot");
        services.Where(d => d.ServiceType == typeof(IConnectionRepoSnapshotStore)).Should().ContainSingle();
        services.Should().Contain(d => d.ServiceType == typeof(RedisConnectionRepoSnapshot));
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
