using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services.Persistence;
using FluentAssertions;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-10-02-5f89c: one hash per connection, read by every replica; a success and a failure own
/// disjoint fields, so neither erases the other's half.
/// </summary>
public sealed class RedisConnectionRepoSnapshotTests
{
    private static readonly DateTimeOffset Morning = new(2026, 10, 2, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Noon = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly DiscoveredRepo Api = new() { Name = "api", Url = "https://git.example.test/api.git", DefaultBranch = "main" };

    [Fact]
    public async Task RedisConnectionRepoSnapshot_WrittenByOneInstance_IsReadByAnother()
    {
        var redis = new FakeRedisHashes();
        var a = new RedisConnectionRepoSnapshot(redis.Replica());
        var b = new RedisConnectionRepoSnapshot(redis.Replica());

        await a.SetAsync("Conn", [Api], Morning, CancellationToken.None);

        b.TryGet("conn", out var hot).Should().BeTrue();
        hot.Should().ContainSingle().Which.Should().Be(Api);
        (await b.TryGetAsync("conn", CancellationToken.None)).Should().ContainSingle();
        var status = await b.TryGetDiscoveryAsync("conn", CancellationToken.None);
        status.Should().Be(status! with { DiscoveredAt = Morning, LastAttemptAt = Morning, LastError = null });
        status.RepoCount.Should().Be(1);
    }

    [Fact]
    public async Task RedisConnectionRepoSnapshot_SuccessAndFailureFromTwoInstances_KeepBothHalves()
    {
        var redis = new FakeRedisHashes();
        var a = new RedisConnectionRepoSnapshot(redis.Replica());
        var b = new RedisConnectionRepoSnapshot(redis.Replica());

        await a.SetAsync("conn", [Api], Morning, CancellationToken.None);
        await b.RecordFailureAsync("conn", "HTTP 401", Noon, CancellationToken.None);

        var status = await a.TryGetDiscoveryAsync("conn", CancellationToken.None);
        status!.DiscoveredAt.Should().Be(Morning);
        status.Repos.Should().ContainSingle();
        status.LastAttemptAt.Should().Be(Noon);
        status.LastError.Should().Be("HTTP 401");
    }

    [Fact]
    public async Task RedisConnectionRepoSnapshot_SuccessAfterAFailure_ClearsTheError()
    {
        var redis = new FakeRedisHashes();
        var store = new RedisConnectionRepoSnapshot(redis.Replica());
        await store.RecordFailureAsync("conn", "HTTP 401", Morning, CancellationToken.None);

        await store.SetAsync("conn", [Api], Noon, CancellationToken.None);

        var status = await store.TryGetDiscoveryAsync("conn", CancellationToken.None);
        status!.LastError.Should().BeNull();
        status.LastAttemptAt.Should().Be(Noon);
    }

    [Fact]
    public async Task RedisConnectionRepoSnapshot_NothingRecorded_IsMissing()
    {
        var store = new RedisConnectionRepoSnapshot(new FakeRedisHashes().Replica());

        store.TryGet("conn", out _).Should().BeFalse();
        (await store.TryGetAsync("conn", CancellationToken.None)).Should().BeNull();
        (await store.TryGetDiscoveryAsync("conn", CancellationToken.None)).Should().BeNull();
    }
}
