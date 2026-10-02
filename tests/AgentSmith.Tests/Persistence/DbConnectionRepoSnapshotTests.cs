using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-10-02-5ab2a: connection discovery in one database row per connection. Each instance
/// stands for one replica — its own 30-second cache, the shared row.
/// </summary>
public sealed class DbConnectionRepoSnapshotTests : IDisposable
{
    private static readonly DateTimeOffset Morning = new(2026, 10, 2, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Noon = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly DiscoveredRepo Api = new() { Name = "api", Url = "https://git.example.test/api.git", DefaultBranch = "main" };
    private static readonly DiscoveredRepo Web = new() { Name = "web", Url = "https://git.example.test/web.git" };

    private readonly SettableClock _clock = new();
    private readonly ServerStateStore _store;

    public DbConnectionRepoSnapshotTests() => _store = new ServerStateStore(_clock);

    [Fact]
    public async Task DbConnectionRepoSnapshot_SuccessAndFailureFromTwoInstances_KeepBothHalves()
    {
        var a = Replica();
        var b = Replica();

        await a.SetAsync("Conn", [Api], Morning, CancellationToken.None);
        await b.RecordFailureAsync("conn", "HTTP 401", Noon, CancellationToken.None);

        var status = await a.TryGetDiscoveryAsync("conn", CancellationToken.None);
        status!.DiscoveredAt.Should().Be(Morning);
        status.Repos.Should().ContainSingle().Which.Should().Be(Api);
        status.LastAttemptAt.Should().Be(Noon);
        status.LastError.Should().Be("HTTP 401");
    }

    [Fact]
    public async Task DbConnectionRepoSnapshot_SuccessAfterAFailure_ClearsTheError()
    {
        var store = Replica();
        await store.RecordFailureAsync("conn", "HTTP 401", Morning, CancellationToken.None);

        await store.SetAsync("conn", [Api], Noon, CancellationToken.None);

        var status = await store.TryGetDiscoveryAsync("conn", CancellationToken.None);
        status!.LastError.Should().BeNull();
        status.LastAttemptAt.Should().Be(Noon);
        status.RepoCount.Should().Be(1);
    }

    [Fact]
    public async Task DbConnectionRepoSnapshot_WrittenByOneInstance_IsReadByAnotherWithin30Seconds()
    {
        var a = Replica();
        var b = Replica();
        await a.SetAsync("conn", [Api], Morning, CancellationToken.None);
        b.TryGet("conn", out var first).Should().BeTrue();
        first.Should().ContainSingle();

        await a.SetAsync("conn", [Api, Web], Noon, CancellationToken.None);
        b.TryGet("conn", out var cached).Should().BeTrue();
        cached.Should().ContainSingle("B's cache entry is younger than 30 seconds");
        _clock.Now += TimeSpan.FromSeconds(30);

        b.TryGet("conn", out var fresh).Should().BeTrue();
        fresh.Should().HaveCount(2);
    }

    [Fact]
    public void DbConnectionRepoSnapshot_OwnSet_IsReadAtOnce()
    {
        var a = Replica();

        a.Set("conn", [Api, Web]);

        a.TryGet("conn", out var repos).Should().BeTrue();
        repos.Should().HaveCount(2);
    }

    [Fact]
    public async Task DbConnectionRepoSnapshot_MissingRow_IsNotCached()
    {
        var a = Replica();
        a.TryGet("conn", out _).Should().BeFalse();

        await Replica().SetAsync("conn", [Api], Morning, CancellationToken.None);

        a.TryGet("conn", out var repos).Should().BeTrue("a miss is read again, not remembered");
        repos.Should().ContainSingle();
        (await a.TryGetAsync("ghost", CancellationToken.None)).Should().BeNull();
        (await a.TryGetDiscoveryAsync("ghost", CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task DbConnectionRepoSnapshot_FailureOnlyRow_ReadsAsMissAndIsNotCached()
    {
        var a = Replica();
        await a.RecordFailureAsync("conn", "HTTP 401", Morning, CancellationToken.None);

        a.TryGet("conn", out _).Should().BeFalse("the expander must still block on its first refresh");
        (await a.TryGetAsync("conn", CancellationToken.None)).Should().BeNull();
        (await a.TryGetDiscoveryAsync("conn", CancellationToken.None))!.RepoCount.Should().BeNull();
        await a.SetAsync("conn", [Api], Noon, CancellationToken.None);

        Replica().TryGet("conn", out _).Should().BeTrue();
        a.TryGet("conn", out _).Should().BeTrue("the failure-only miss was not cached");
    }

    [Fact]
    public async Task DbConnectionRepoSnapshot_ConcurrentFirstInserts_BothLand()
    {
        var fired = false;
        DbConnectionRepoSnapshot? winner = null;
        using var racing = new ServerStateStore(_clock, db => new RacingUnitOfWork(db, async () =>
        {
            if (fired) return;
            fired = true;
            await winner!.SetAsync("conn", [Api], Morning, CancellationToken.None);
        }));
        winner = new DbConnectionRepoSnapshot(racing.ScopeFactory, _clock);
        var loser = new DbConnectionRepoSnapshot(racing.ScopeFactory, _clock);

        await loser.RecordFailureAsync("conn", "HTTP 401", Noon, CancellationToken.None);

        var status = await winner.TryGetDiscoveryAsync("conn", CancellationToken.None);
        status!.Repos.Should().ContainSingle("the winner's insert stands");
        status.LastError.Should().Be("HTTP 401", "the loser's insert turned into its update");
        fired.Should().BeTrue();
        await using var check = racing.Context();
        (await check.Set<ConnectionDiscovery>().CountAsync()).Should().Be(1);
    }

    private DbConnectionRepoSnapshot Replica() => new(_store.ScopeFactory, _clock);

    public void Dispose() => _store.Dispose();
}
