using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Services.Providers.Discovery;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AgentSmith.Tests.Services;

public class RepoDiscoveryRefresherTests
{
    private readonly InMemoryConnectionRepoSnapshot _snapshot = new();
    private readonly FakeStore _store = new();
    private static readonly ResolvedConnection Conn = new() { Name = "conn", Type = RepoType.AzureDevOps };

    [Fact]
    public async Task Refresh_Success_UpdatesSnapshotAndDurableStore()
    {
        var repos = new[] { new DiscoveredRepo { Name = "a" } };
        var refresher = Build(new OkDiscovery(repos));

        await refresher.RefreshAsync(Conn, CancellationToken.None);

        _snapshot.TryGet("conn", out var hot).Should().BeTrue();
        hot.Should().HaveCount(1);
        (await _store.TryGetAsync("conn", CancellationToken.None))!.Should().HaveCount(1);
    }

    [Fact]
    public async Task Refresh_DiscoveryFails_WithLastGood_ServesStaleSnapshot_NoThrow()
    {
        await _store.SetAsync("conn", new[] { new DiscoveredRepo { Name = "old" } }, CancellationToken.None);
        var refresher = Build(new FailingDiscovery());

        await refresher.RefreshAsync(Conn, CancellationToken.None);   // must not throw

        _snapshot.TryGet("conn", out var hot).Should().BeTrue();
        hot.Single().Name.Should().Be("old");
    }

    [Fact]
    public async Task Refresh_DiscoveryFails_ColdCache_FailsLoud()
    {
        var refresher = Build(new FailingDiscovery());

        var act = async () => await refresher.RefreshAsync(Conn, CancellationToken.None);

        await act.Should().ThrowAsync<ConfigurationException>().WithMessage("*cold cache*");
    }

    private RepoDiscoveryRefresher Build(IRepoDiscoveryService discovery) =>
        new(discovery, _snapshot, _store, NullLogger<RepoDiscoveryRefresher>.Instance);

    private sealed class OkDiscovery(IReadOnlyList<DiscoveredRepo> repos) : IRepoDiscoveryService
    {
        public Task<IReadOnlyList<DiscoveredRepo>> DiscoverAsync(ResolvedConnection c, CancellationToken ct) =>
            Task.FromResult(repos);
    }

    private sealed class FailingDiscovery : IRepoDiscoveryService
    {
        public Task<IReadOnlyList<DiscoveredRepo>> DiscoverAsync(ResolvedConnection c, CancellationToken ct) =>
            throw new InvalidOperationException("HTTP 401");
    }

    private sealed class FakeStore : IConnectionRepoSnapshotStore
    {
        private readonly Dictionary<string, IReadOnlyList<DiscoveredRepo>> _data = new();
        public Task<IReadOnlyList<DiscoveredRepo>?> TryGetAsync(string name, CancellationToken ct) =>
            Task.FromResult(_data.TryGetValue(name, out var v) ? v : null);
        public Task SetAsync(string name, IReadOnlyList<DiscoveredRepo> repos, CancellationToken ct)
        {
            _data[name] = repos;
            return Task.CompletedTask;
        }
    }
}
