using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Services.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-10-02-b540: Redis for what replicas share, the pod's disk for what must survive a Redis
/// flush or outage. A success lands in both, a failure only in Redis; a read falls back to disk
/// and says so.
/// </summary>
public sealed class ConnectionDiscoveryStoreTests : IDisposable
{
    private static readonly DateTimeOffset Morning = new(2026, 10, 2, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Noon = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly DiscoveredRepo Api = new() { Name = "api", Url = "https://git.example.test/api.git" };
    private const string Key = "agentsmith:discovery:conn";

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"agentsmith-b540-{Guid.NewGuid():N}");
    private readonly FakeRedisHashes _redis = new();

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task DiscoveryStore_Success_WritesRedisAndDisk()
    {
        await Store().SetAsync("conn", [Api], Morning, CancellationToken.None);

        (await new RedisConnectionRepoSnapshot(_redis.Replica()).TryGetAsync("conn", CancellationToken.None))
            .Should().ContainSingle().Which.Should().Be(Api);
        (await Disk().TryGetAsync("conn", CancellationToken.None)).Should().ContainSingle().Which.Should().Be(Api);
        var read = Store().TryRead("conn");
        read!.Source.Should().Be(ConnectionDiscoverySource.Shared);
    }

    [Fact]
    public async Task DiscoveryStore_RedisUnreachable_ServesTheDiskLastGoodList()
    {
        await Store().SetAsync("conn", [Api], Morning, CancellationToken.None);
        _redis.Down = true;
        var store = Store();

        var read = store.TryRead("conn");
        read!.Repos.Should().ContainSingle().Which.Should().Be(Api);
        read.Source.Should().Be(ConnectionDiscoverySource.Local);
        (await store.TryGetAsync("conn", CancellationToken.None)).Should().ContainSingle();
        var status = await store.TryGetDiscoveryAsync("conn", CancellationToken.None);
        status!.Source.Should().Be(ConnectionDiscoverySource.Local);
        status.DiscoveredAt.Should().Be(Morning);

        var writes = async () =>
        {
            await store.RecordFailureAsync("conn", "HTTP 503", Noon, CancellationToken.None);
            await store.SetAsync("conn", [Api, Api with { Name = "web" }], Noon, CancellationToken.None);
        };
        await writes.Should().NotThrowAsync();
        (await Disk().TryGetAsync("conn", CancellationToken.None)).Should().HaveCount(2);
    }

    [Fact]
    public async Task DiscoveryStore_RedisKeyMissing_ServesDiskWithTheSharedError()
    {
        await Store().SetAsync("conn", [Api], Morning, CancellationToken.None);
        _redis.Flush(Key);
        Store().TryRead("conn")!.Source.Should().Be(ConnectionDiscoverySource.Local);

        await Store().RecordFailureAsync("conn", "HTTP 401", Noon, CancellationToken.None);

        var status = await Store().TryGetDiscoveryAsync("conn", CancellationToken.None);
        status!.Source.Should().Be(ConnectionDiscoverySource.Local);
        status.DiscoveredAt.Should().Be(Morning);
        status.RepoCount.Should().Be(1);
        status.LastAttemptAt.Should().Be(Noon);
        status.LastError.Should().Be("HTTP 401");
    }

    [Fact]
    public async Task DiscoveryStore_Failure_NeverOverwritesTheDiskList()
    {
        await Store().SetAsync("conn", [Api], Morning, CancellationToken.None);

        await Store().RecordFailureAsync("conn", "HTTP 401", Noon, CancellationToken.None);

        var disk = await Disk().TryGetDiscoveryAsync("conn", CancellationToken.None);
        disk!.Repos.Should().ContainSingle().Which.Should().Be(Api);
        disk.LastError.Should().BeNull();
        disk.LastAttemptAt.Should().Be(Morning);
        (await Store().TryGetDiscoveryAsync("conn", CancellationToken.None))!.LastError.Should().Be("HTTP 401");
    }

    [Fact]
    public void DiscoveryStore_NothingAnywhere_IsMissing() => Store().TryRead("conn").Should().BeNull();

    // One store per call stands for one server process: its own disk, the shared Redis.
    private ConnectionDiscoveryStore Store() => new(
        new RedisConnectionRepoSnapshot(_redis.Replica()), Disk(), NullLogger<ConnectionDiscoveryStore>.Instance);

    private DiskConnectionRepoSnapshotStore Disk() =>
        new(new DiscoveryTempPaths(_root), NullLogger<DiskConnectionRepoSnapshotStore>.Instance);
}
