using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Services.Providers.Discovery;
using AgentSmith.Server.Services.Config;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// 2026-10-02-5f89c: the studio's discovery read over the shared store — a missing key is
/// discovered at once, Refresh now on one replica is read back on another, an unknown id is 404.
/// 2026-10-02-5ab2a: the shared store is the database row.
/// </summary>
public sealed class ConnectionDiscoveryEndpointsTests : IDisposable
{
    private static readonly ResolvedConnection Conn = new() { Name = "conn", Type = RepoType.GitLab, Group = "acme" };
    private readonly ServerStateStore _db = new();
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

    // One reader per call stands for one replica: its own process state, the shared row.
    private ConnectionDiscoveryReader Reader()
    {
        var store = new DbConnectionRepoSnapshot(_db.ScopeFactory, TimeProvider.System);
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

    public void Dispose() => _db.Dispose();
}
