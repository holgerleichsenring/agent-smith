using System.Text.Json;
using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Services.Events;
using AgentSmith.Server.Services.Events;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using StackExchange.Redis;

namespace AgentSmith.Tests.Hubs;

/// <summary>
/// p0169h contract tests for the trail-reader logic. Uses a mocked Redis
/// database (same pattern as RedisEventPublisherTests) to verify:
///   (1) ReadAllAsync issues XRANGE "-" "+" and deserialises every entry
///   (2) ReadPageAsync clamps the count to [1, 2000]
///   (3) Pagination sets HasMore when the stream has more than `count`
///       entries and returns the last id as NextCursor
/// p0286: when the Redis stream is gone the reader falls back to the durable DB
/// trail — exercised on a real in-memory SQLite engine.
/// </summary>
public sealed class TrailReaderTests : IDisposable
{
    private readonly Mock<IConnectionMultiplexer> _redis = new();
    private readonly Mock<IDatabase> _db = new();
    private readonly SqliteConnection _connection;
    private readonly IServiceScopeFactory _scopes;
    private readonly string _runId = "2026-05-27T12-00-00-aaaa";

    public TrailReaderTests()
    {
        _redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(_db.Object);
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using (var ctx = new AgentSmithDbContext(Options())) ctx.Database.Migrate();
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(Options()));
        _scopes = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    public void Dispose() => _connection.Dispose();

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    // Mirrors RunDbProjector.BuildTrail: raw STJ payload + the EventType name, so
    // the reader's DeserializeRaw path matches what the projector actually writes.
    private void SeedDbTrail(params RunEvent[] events)
    {
        using var ctx = new AgentSmithDbContext(Options());
        long seq = 0;
        foreach (var ev in events)
            ctx.RunEvents.Add(new AgentSmith.Infrastructure.Persistence.Entities.RunEvent
            {
                RunId = _runId,
                Seq = seq++,
                Type = ev.Type.ToString(),
                Timestamp = ev.Timestamp,
                PayloadJson = JsonSerializer.Serialize((object)ev, ev.GetType()),
            });
        ctx.SaveChanges();
    }

    private void StubEmptyRedis() => _db.Setup(d => d.StreamRangeAsync(
            (RedisKey)EventStreamKeys.RunStream(_runId), "-", "+", null, Order.Ascending, CommandFlags.None))
        .ReturnsAsync(Array.Empty<StreamEntry>());

    [Fact]
    public async Task ReadAllTypedAsync_RedisEmpty_FallsBackToDurableDbTrail()
    {
        StubEmptyRedis();
        SeedDbTrail(
            new RunStartedEvent(_runId, "ticket", "fix-bug", new[] { "server" }, DateTimeOffset.UtcNow),
            new StepStartedEvent(_runId, 1, "CheckoutSource", 10, DateTimeOffset.UtcNow),
            new RunFinishedEvent(_runId, "success", null, "ok", DateTimeOffset.UtcNow));

        var sut = new TrailReader(_redis.Object, _scopes);
        var result = await sut.ReadAllTypedAsync(_runId);

        result.Select(e => e.Type).Should().ContainInOrder(
            EventType.RunStarted, EventType.StepStarted, EventType.RunFinished);
    }

    [Fact]
    public async Task ReadDbTrailTypedAsync_AlwaysReadsDb_EvenWhenRedisHasEntries()
    {
        // Redis still holds events — but ReadDbTrail must IGNORE it and source the
        // structural skeleton from the durable DB (p0288: the execution-tree path).
        _db.Setup(d => d.StreamRangeAsync(
                (RedisKey)EventStreamKeys.RunStream(_runId), "-", "+", null, Order.Ascending, CommandFlags.None))
            .ReturnsAsync(new[] { EntryFor(new RunStartedEvent(_runId, "t", "p", new[] { "r" }, DateTimeOffset.UtcNow)) });
        SeedDbTrail(
            new RunStartedEvent(_runId, "ticket", "fix-bug", new[] { "server" }, DateTimeOffset.UtcNow),
            new StepStartedEvent(_runId, 1, "CheckoutSource", 10, DateTimeOffset.UtcNow),
            new StepStartedEvent(_runId, 2, "AnalyzeCode", 20, DateTimeOffset.UtcNow),
            new RunFinishedEvent(_runId, "success", null, "ok", DateTimeOffset.UtcNow));

        var sut = new TrailReader(_redis.Object, _scopes);
        var result = await sut.ReadDbTrailTypedAsync(_runId);

        // All 4 DB rows (not the single Redis entry) come back, ordered by Seq.
        result.Select(e => e.Type).Should().ContainInOrder(
            EventType.RunStarted, EventType.StepStarted, EventType.StepStarted, EventType.RunFinished);
    }

    [Fact]
    public async Task ReadAllAsync_ReturnsDeserialisedEventsInOrder()
    {
        var entries = new[]
        {
            EntryFor(new RunStartedEvent(_runId, "ticket", "fix-bug",
                new[] { "server" }, DateTimeOffset.UtcNow)),
            EntryFor(new StepStartedEvent(_runId, 1, "CheckoutSource", 10, DateTimeOffset.UtcNow)),
            EntryFor(new RunFinishedEvent(_runId, "success", null, "ok", DateTimeOffset.UtcNow)),
        };
        _db.Setup(d => d.StreamRangeAsync(
            (RedisKey)EventStreamKeys.RunStream(_runId), "-", "+", null, Order.Ascending, CommandFlags.None))
            .ReturnsAsync(entries);

        var sut = new TrailReader(_redis.Object, _scopes);
        var result = await sut.ReadAllTypedAsync(_runId);

        result.Should().HaveCount(3);
        result.Select(e => e.Type).Should().ContainInOrder(
            EventType.RunStarted, EventType.StepStarted, EventType.RunFinished);
    }

    [Fact]
    public async Task ReadAllAsync_StreamMissing_ReturnsEmpty()
    {
        _db.Setup(d => d.StreamRangeAsync(
            (RedisKey)EventStreamKeys.RunStream(_runId), "-", "+", null, Order.Ascending, CommandFlags.None))
            .ReturnsAsync(Array.Empty<StreamEntry>());

        var sut = new TrailReader(_redis.Object, _scopes);
        var result = await sut.ReadAllTypedAsync(_runId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadPageAsync_ClampsCountToOneAtLowerBound()
    {
        _db.Setup(d => d.StreamRangeAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(),
            It.IsAny<int?>(), It.IsAny<Order>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(Array.Empty<StreamEntry>());

        var sut = new TrailReader(_redis.Object, _scopes);
        await sut.ReadPageAsync(_runId, fromId: null, count: 0);

        _db.Verify(d => d.StreamRangeAsync(
            It.IsAny<RedisKey>(), (RedisValue)"-", (RedisValue)"+", 2,
            Order.Ascending, CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task ReadPageAsync_DetectsHasMoreAndStripsExtraEntry()
    {
        var entries = new[]
        {
            EntryFor(new RunStartedEvent(_runId, "t", "p", new[] { "r" }, DateTimeOffset.UtcNow), "1-0"),
            EntryFor(new StepStartedEvent(_runId, 1, "Step", 10, DateTimeOffset.UtcNow), "2-0"),
            EntryFor(new StepFinishedEvent(_runId, 1, "success", 100, DateTimeOffset.UtcNow), "3-0"),
        };
        _db.Setup(d => d.StreamRangeAsync(
            (RedisKey)EventStreamKeys.RunStream(_runId), "-", "+", 3,
            Order.Ascending, CommandFlags.None))
            .ReturnsAsync(entries);

        var sut = new TrailReader(_redis.Object, _scopes);
        var page = await sut.ReadPageAsync(_runId, fromId: null, count: 2);

        page.Events.Should().HaveCount(2);
        page.HasMore.Should().BeTrue();
        page.NextCursor.Should().Be("2-0");
    }

    [Fact]
    public async Task ReadPageAsync_FewerEntriesThanCount_HasMoreFalse()
    {
        var entries = new[]
        {
            EntryFor(new RunStartedEvent(_runId, "t", "p", new[] { "r" }, DateTimeOffset.UtcNow), "1-0"),
        };
        _db.Setup(d => d.StreamRangeAsync(
            (RedisKey)EventStreamKeys.RunStream(_runId), "-", "+", 501,
            Order.Ascending, CommandFlags.None))
            .ReturnsAsync(entries);

        var sut = new TrailReader(_redis.Object, _scopes);
        var page = await sut.ReadPageAsync(_runId, fromId: null, count: null);

        page.HasMore.Should().BeFalse();
        page.Events.Should().HaveCount(1);
        page.NextCursor.Should().Be("1-0");
    }

    [Fact]
    public async Task ReadPageAsync_ClampsCountToTwoThousandAtUpperBound()
    {
        _db.Setup(d => d.StreamRangeAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(),
            2001, Order.Ascending, CommandFlags.None))
            .ReturnsAsync(Array.Empty<StreamEntry>());

        var sut = new TrailReader(_redis.Object, _scopes);
        await sut.ReadPageAsync(_runId, fromId: null, count: 999_999);

        _db.Verify(d => d.StreamRangeAsync(
            It.IsAny<RedisKey>(), (RedisValue)"-", (RedisValue)"+", 2001,
            Order.Ascending, CommandFlags.None), Times.Once);
    }

    private static StreamEntry EntryFor(RunEvent evt, string id = "0-0")
    {
        var payload = EventEnvelopeSerializer.Serialize(evt);
        return new StreamEntry(id, new[] { new NameValueEntry("e", payload) });
    }
}
