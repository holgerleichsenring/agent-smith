using AgentSmith.Contracts.Exceptions;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Services.Providers.Discovery;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0281a, 2026-10-02-5f89c: the refresher over the real disk store — every outcome recorded,
/// the last success kept through a failure, one discovery per connection at a time.
/// </summary>
public sealed class RepoDiscoveryRefresherTests : IDisposable
{
    private readonly InMemoryConnectionRepoSnapshot _snapshot = new();
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"agentsmith-discovery-{Guid.NewGuid():N}");
    private readonly DiskConnectionRepoSnapshotStore _store;
    private static readonly ResolvedConnection Conn = new() { Name = "conn", Type = RepoType.AzureDevOps };
    private static readonly DateTimeOffset Yesterday = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    public RepoDiscoveryRefresherTests() =>
        _store = new DiskConnectionRepoSnapshotStore(new TempPaths(_root), NullLogger<DiskConnectionRepoSnapshotStore>.Instance);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task Refresh_Success_UpdatesSnapshotAndDurableStore()
    {
        var refresher = Build(new ScriptedDiscovery(_ => Task.FromResult<IReadOnlyList<DiscoveredRepo>>([new DiscoveredRepo { Name = "a" }])));

        await refresher.RefreshAsync(Conn, CancellationToken.None);

        _snapshot.TryRead("conn")!.Repos.Should().HaveCount(1);
        var status = await _store.TryGetDiscoveryAsync("conn", CancellationToken.None);
        status!.RepoCount.Should().Be(1);
        status.LastError.Should().BeNull();
    }

    [Fact]
    public async Task RepoDiscoveryRefresher_Failure_KeepsLastSuccessAndRecordsTheError()
    {
        await _store.SetAsync("conn", [new DiscoveredRepo { Name = "old" }], Yesterday, CancellationToken.None);
        var refresher = Build(new ScriptedDiscovery(_ => throw new InvalidOperationException("discovery failed: HTTP 401.")));

        await refresher.RefreshAsync(Conn, CancellationToken.None);   // must not throw

        _snapshot.TryRead("conn")!.Repos.Single().Name.Should().Be("old");
        var status = await _store.TryGetDiscoveryAsync("conn", CancellationToken.None);
        status!.DiscoveredAt.Should().Be(Yesterday);
        status.RepoCount.Should().Be(1);
        status.LastError.Should().Be("discovery failed: HTTP 401.");
        status.LastAttemptAt.Should().BeAfter(Yesterday);
    }

    [Fact]
    public async Task RepoDiscoveryRefresher_MissingCredential_ServesLastGoodAndRecordsIt()
    {
        await _store.SetAsync("conn", [new DiscoveredRepo { Name = "old" }], Yesterday, CancellationToken.None);
        var refresher = Build(new ScriptedDiscovery(_ => throw new MissingCredentialException("Connection 'conn'", "gitlab_b")));

        await refresher.RefreshAsync(Conn, CancellationToken.None);

        _snapshot.TryRead("conn")!.Repos.Single().Name.Should().Be("old");
        (await _store.TryGetDiscoveryAsync("conn", CancellationToken.None))!.LastError.Should().Contain("'gitlab_b'");
    }

    [Fact]
    public async Task Refresh_OtherConfigurationError_IsRecordedAndThrown()
    {
        var refresher = Build(new ScriptedDiscovery(_ => throw new ConfigurationException("requires 'group'")));

        var act = async () => await refresher.RefreshAsync(Conn, CancellationToken.None);

        await act.Should().ThrowAsync<ConfigurationException>().WithMessage("requires 'group'");
        (await _store.TryGetDiscoveryAsync("conn", CancellationToken.None))!.LastError.Should().Be("requires 'group'");
    }

    [Fact]
    public async Task Refresh_DiscoveryFails_ColdCache_FailsLoud()
    {
        var refresher = Build(new ScriptedDiscovery(_ => throw new InvalidOperationException("HTTP 401")));

        var act = async () => await refresher.RefreshAsync(Conn, CancellationToken.None);

        await act.Should().ThrowAsync<ConfigurationException>().WithMessage("*cold cache*");
    }

    [Fact]
    public async Task RepoDiscoveryRefresher_ConcurrentRefreshes_OfOneConnection_RunDiscoveryOnce()
    {
        var entered = new TaskCompletionSource();
        var release = new TaskCompletionSource<IReadOnlyList<DiscoveredRepo>>();
        var discovery = new ScriptedDiscovery(_ => { entered.TrySetResult(); return release.Task; });
        var refresher = Build(discovery);

        var first = refresher.RefreshAsync(Conn, CancellationToken.None);
        var second = refresher.RefreshAsync(Conn, CancellationToken.None);
        await entered.Task;
        release.SetResult([new DiscoveredRepo { Name = "a" }]);
        await Task.WhenAll(first, second);

        discovery.Calls.Should().Be(1);
    }

    [Fact]
    public async Task ConnectionRefreshFlights_ARefreshAskingForItselfOnItsOwnFlow_RunsInsteadOfWaitingOnItself()
    {
        var flights = new ConnectionRefreshFlights();
        var inner = 0;

        await flights.RunAsync("conn", () => flights.RunAsync("conn", () => { inner++; return Task.CompletedTask; },
            CancellationToken.None), CancellationToken.None);

        inner.Should().Be(1);
    }

    private RepoDiscoveryRefresher Build(IRepoDiscoveryService discovery) =>
        new(discovery, _snapshot, _store, new ConnectionRefreshFlights(), TimeProvider.System,
            NullLogger<RepoDiscoveryRefresher>.Instance);

    private sealed class ScriptedDiscovery(Func<ResolvedConnection, Task<IReadOnlyList<DiscoveredRepo>>> answer)
        : IRepoDiscoveryService
    {
        private int _calls;
        public int Calls => _calls;

        public Task<IReadOnlyList<DiscoveredRepo>> DiscoverAsync(ResolvedConnection c, CancellationToken ct)
        {
            Interlocked.Increment(ref _calls);
            return answer(c);
        }
    }

    private sealed record TempPaths(string CacheRoot) : IAgentSmithPaths
    {
        public string SkillsCatalogRoot => Path.Combine(CacheRoot, "skills");
        public string ProjectCacheDir(string repositoryRemoteUrl) => Path.Combine(CacheRoot, "projects");
    }
}
