using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Services.Persistence;
using AgentSmith.Infrastructure.Services.Providers.Discovery;
using AgentSmith.Server.Services.Config;
using AgentSmith.Tests.Persistence;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// 2026-10-02-5f89c: the studio's discovery read over the shared Redis hash — a missing key is
/// discovered at once, Refresh now on one replica is read back on another, an unknown id is 404.
/// </summary>
public sealed class ConnectionDiscoveryEndpointsTests
{
    private static readonly ResolvedConnection Conn = new() { Name = "conn", Type = RepoType.GitLab, Group = "acme" };
    private readonly FakeRedisHashes _redis = new();
    private int _discoveries;
    private Func<IReadOnlyList<DiscoveredRepo>> _answer = () => [new DiscoveredRepo { Name = "api" }];

    [Fact]
    public async Task ConnectionDiscoveryEndpoints_MissingKey_RefreshesImmediately()
    {
        var view = await Reader().ReadAsync("conn", CancellationToken.None);

        _discoveries.Should().Be(1);
        view!.RepoCount.Should().Be(1);
        view.Repos.Should().ContainSingle().Which.Name.Should().Be("api");
        view.Discovering.Should().BeFalse();
    }

    [Fact]
    public async Task ConnectionDiscovery_KeyPresent_IsServedWithoutDiscovering()
    {
        await Reader().ReadAsync("conn", CancellationToken.None);

        await Reader().ReadAsync("conn", CancellationToken.None);

        _discoveries.Should().Be(1);
    }

    [Fact]
    public async Task ConnectionDiscovery_RefreshNowAfterAFix_ClearsTheErrorForEveryReplica()
    {
        await Reader().ReadAsync("conn", CancellationToken.None);
        _answer = () => throw new InvalidOperationException("GitLab repo discovery for 'conn' failed: HTTP 401.");
        var failed = await Reader().RefreshNowAsync("conn", CancellationToken.None);
        failed!.LastError.Should().Contain("HTTP 401");
        failed.RepoCount.Should().Be(1);

        _answer = () => [new DiscoveredRepo { Name = "api" }, new DiscoveredRepo { Name = "web" }];
        await Reader().RefreshNowAsync("conn", CancellationToken.None);

        var elsewhere = await Reader().ReadAsync("conn", CancellationToken.None);
        elsewhere!.LastError.Should().BeNull();
        elsewhere.RepoCount.Should().Be(2);
    }

    [Fact]
    public async Task ConnectionDiscovery_UnknownConnection_IsNull()
    {
        (await Reader().ReadAsync("ghost", CancellationToken.None)).Should().BeNull();
        (await Reader().RefreshNowAsync("ghost", CancellationToken.None)).Should().BeNull();
    }

    // 2026-10-02-b540: a flushed key answers this server's last good list at once, marked local,
    // and the refresh that writes the key back starts behind it.
    [Fact]
    public async Task DiscoveryStore_RedisKeyMissing_ServesDiskAndStartsARefresh()
    {
        var root = Path.Combine(Path.GetTempPath(), $"agentsmith-b540-{Guid.NewGuid():N}");
        try
        {
            var store = new ConnectionDiscoveryStore(new RedisConnectionRepoSnapshot(_redis.Replica()),
                new DiskConnectionRepoSnapshotStore(new DiscoveryTempPaths(root), NullLogger<DiskConnectionRepoSnapshotStore>.Instance),
                NullLogger<ConnectionDiscoveryStore>.Instance);
            await Reader(store).ReadAsync("conn", CancellationToken.None);
            _redis.Flush("agentsmith:discovery:conn");

            var view = await Reader(store).ReadAsync("conn", CancellationToken.None);

            view!.Source.Should().Be("local");
            view.RepoCount.Should().Be(1);
            var shared = new RedisConnectionRepoSnapshot(_redis.Replica());
            (await TestWaits.ReachedAsync(() => shared.TryRead("conn") is not null))
                .Should().BeTrue("the refresh started behind the local answer writes the key back");
            _discoveries.Should().Be(2);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    // One reader per call stands for one replica: its own process state, the shared hash.
    private ConnectionDiscoveryReader Reader() => Reader(new RedisConnectionRepoSnapshot(_redis.Replica()));

    private ConnectionDiscoveryReader Reader<TStore>(TStore store)
        where TStore : IConnectionRepoSnapshot, IConnectionRepoSnapshotStore
    {
        var discovery = new Mock<IRepoDiscoveryService>();
        discovery.Setup(d => d.DiscoverAsync(It.IsAny<ResolvedConnection>(), It.IsAny<CancellationToken>()))
            .Returns(() => { _discoveries++; return Task.FromResult(_answer()); });
        var refresher = new RepoDiscoveryRefresher(discovery.Object, store, store, new ConnectionRefreshFlights(),
            TimeProvider.System, NullLogger<RepoDiscoveryRefresher>.Instance);
        var configStore = Mock.Of<IConfigStore>(s =>
            s.GetConnections() == new List<ConnectionEntity> { new("conn", "gitlab", "acme", null, "gitlab_a", null) });
        var config = new AgentSmithConfig { Connections = new() { ["conn"] = Conn } };
        var loader = Mock.Of<IConfigurationLoader>(l => l.LoadConfig(It.IsAny<string>()) == config);
        return new ConnectionDiscoveryReader(configStore, loader, store, refresher);
    }
}
