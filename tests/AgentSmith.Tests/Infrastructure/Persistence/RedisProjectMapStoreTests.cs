using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace AgentSmith.Tests.Infrastructure.Persistence;

public sealed class RedisProjectMapStoreTests
{
    private readonly Mock<IConnectionMultiplexer> _redis = new();
    private readonly Mock<IDatabase> _db = new();
    private readonly RedisProjectMapStore _sut;
    private readonly Dictionary<string, (string Value, TimeSpan? Ttl)> _store = new(StringComparer.Ordinal);

    public RedisProjectMapStoreTests()
    {
        _redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_db.Object);

        _db.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey k, CommandFlags _) =>
                _store.TryGetValue(k.ToString(), out var v) ? new RedisValue(v.Value) : RedisValue.Null);

        _db.Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey k, RedisValue v, TimeSpan? ttl, bool _, When _, CommandFlags _) =>
            {
                _store[k.ToString()] = (v.ToString(), ttl);
                return true;
            });

        _db.Setup(d => d.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey k, TimeSpan? ttl, ExpireWhen _, CommandFlags _) =>
            {
                if (!_store.TryGetValue(k.ToString(), out var existing)) return false;
                _store[k.ToString()] = (existing.Value, ttl);
                return true;
            });

        _sut = new RedisProjectMapStore(
            _redis.Object,
            NullLogger<RedisProjectMapStore>.Instance);
    }

    [Fact]
    public async Task RedisProjectMapStore_SetThenGet_SameHash_RoundTrips()
    {
        var map = SampleMap();
        await _sut.SetAsync("sandbox-a", "hash-1", map, CancellationToken.None);

        var result = await _sut.TryGetAsync("sandbox-a", "hash-1", CancellationToken.None);

        result.Should().NotBeNull();
        result!.PrimaryLanguage.Should().Be(map.PrimaryLanguage);
        result.Modules.Should().HaveSameCount(map.Modules);
    }

    [Fact]
    public async Task RedisProjectMapStore_GetWithMismatchedHash_ReturnsNull()
    {
        await _sut.SetAsync("sandbox-a", "hash-1", SampleMap(), CancellationToken.None);

        var result = await _sut.TryGetAsync("sandbox-a", "DIFFERENT-HASH", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RedisProjectMapStore_Set_AppliesThirtyDayTtl()
    {
        await _sut.SetAsync("sandbox-a", "hash-1", SampleMap(), CancellationToken.None);

        _store["agentsmith:projectmap:sandbox-a"].Ttl.Should().Be(TimeSpan.FromDays(30));
    }

    [Fact]
    public async Task RedisProjectMapStore_GetUnknownKey_ReturnsNull()
    {
        var result = await _sut.TryGetAsync("never-written", "hash", CancellationToken.None);
        result.Should().BeNull();
    }

    private static ProjectMap SampleMap() => new(
        PrimaryLanguage: "C#",
        Frameworks: ["dotnet"],
        Modules: [new Module("src/api", ModuleRole.Production, [])],
        TestProjects: [],
        EntryPoints: [],
        Conventions: new Conventions(null, null, null),
        Ci: new CiConfig(false, null, null, null));
}
