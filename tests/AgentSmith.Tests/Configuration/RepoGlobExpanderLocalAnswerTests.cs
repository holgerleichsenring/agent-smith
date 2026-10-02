using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Services.Persistence;
using AgentSmith.Tests.Persistence;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Configuration;

/// <summary>
/// 2026-10-02-b540: with Redis stopped, a glob over a discovered connection still expands from
/// this server's last good list — synchronously, inside the configuration load — and a refresh
/// starts behind the local answer.
/// </summary>
public sealed class RepoGlobExpanderLocalAnswerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"agentsmith-b540-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task Expand_RedisStopped_ExpandsFromTheLastGoodListAndStartsARefresh()
    {
        var redis = new FakeRedisHashes();
        var store = new ConnectionDiscoveryStore(new RedisConnectionRepoSnapshot(redis.Replica()),
            new DiskConnectionRepoSnapshotStore(new DiscoveryTempPaths(_root), NullLogger<DiskConnectionRepoSnapshotStore>.Instance),
            NullLogger<ConnectionDiscoveryStore>.Instance);
        await store.SetAsync("conn", [Repo("Sample.Api"), Repo("Sample.Web"), Repo("Other")], DateTimeOffset.UtcNow, CancellationToken.None);
        redis.Down = true;
        var refresher = new SignallingRefresher();
        var expander = new RepoGlobExpander(store, refresher, NullLogger<RepoGlobExpander>.Instance);

        var result = expander.Expand("p", [RepoGlobRef.Parse("conn/Sample.*")], Connections());

        result.Select(r => r.Name).Should().BeEquivalentTo("Sample.Api", "Sample.Web");
        (await refresher.Started.Task.OrHang("the refresh behind the local answer")).Should().Be("conn");
    }

    private static DiscoveredRepo Repo(string name) => new() { Name = name, Url = $"https://git.example.test/{name}.git" };

    private static Dictionary<string, ResolvedConnection> Connections() => new()
    {
        ["conn"] = new ResolvedConnection { Name = "conn", Type = RepoType.GitLab, Group = "acme" },
    };

    private sealed class SignallingRefresher : IRepoDiscoveryRefresher
    {
        public TaskCompletionSource<string> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task RefreshAsync(ResolvedConnection connection, CancellationToken cancellationToken)
        {
            Started.TrySetResult(connection.Name);
            return Task.CompletedTask;
        }

        public Task RefreshAllAsync(IReadOnlyCollection<ResolvedConnection> connections, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
