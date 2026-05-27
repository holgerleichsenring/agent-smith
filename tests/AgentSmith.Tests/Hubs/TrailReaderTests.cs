using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Services.Events;
using AgentSmith.Server.Services.Events;
using FluentAssertions;
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
/// </summary>
public sealed class TrailReaderTests
{
    private readonly Mock<IConnectionMultiplexer> _redis = new();
    private readonly Mock<IDatabase> _db = new();
    private readonly string _runId = "2026-05-27T12-00-00-aaaa";

    public TrailReaderTests()
    {
        _redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(_db.Object);
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

        var sut = new TrailReader(_redis.Object);
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

        var sut = new TrailReader(_redis.Object);
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

        var sut = new TrailReader(_redis.Object);
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

        var sut = new TrailReader(_redis.Object);
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

        var sut = new TrailReader(_redis.Object);
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

        var sut = new TrailReader(_redis.Object);
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
