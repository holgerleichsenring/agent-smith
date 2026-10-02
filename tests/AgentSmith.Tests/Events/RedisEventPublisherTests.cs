using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Services.Events;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace AgentSmith.Tests.Events;

/// <summary>
/// p0169e contract tests for the Redis publisher. The mocked DB asserts:
/// (1) stream key follows run:{runId}:events;
/// (2) MAXLEN + TTL applied per append;
/// (3) RunStarted adds runId to the active SET;
/// (4) RunFinished removes from active SET and LPUSH+LTRIM the recent LIST, in one transaction;
/// (5) missing runId throws.
/// </summary>
public sealed class RedisEventPublisherTests
{
    private readonly Mock<IConnectionMultiplexer> _redis = new();
    private readonly Mock<IDatabase> _db = new();

    public RedisEventPublisherTests()
    {
        _redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(_db.Object);
    }

    [Fact]
    public async Task PublishRunStarted_AppendsToPerRunStreamAndAddsToActiveSet()
    {
        var sut = new RedisEventPublisher(_redis.Object, new AgentSmith.Infrastructure.Services.Events.EventEnvelopeSerializer(), NullLogger<RedisEventPublisher>.Instance);
        var ev = new RunStartedEvent("run-1", "ticket", "code",
            new[] { "repo" }, DateTimeOffset.UtcNow);

        await sut.PublishAsync(ev);

        _db.Verify(d => d.StreamAddAsync(
            (RedisKey)"run:run-1:events",
            It.IsAny<NameValueEntry[]>(),
            null,
            EventStreamKeys.StreamMaxLen,
            true,
            CommandFlags.None), Times.Once);
        _db.Verify(d => d.KeyExpireAsync(
            (RedisKey)"run:run-1:events", EventStreamKeys.StreamTtl,
            ExpireWhen.Always, CommandFlags.None), Times.Once);
        _db.Verify(d => d.SetAddAsync(
            (RedisKey)EventStreamKeys.ActiveRunsSet, (RedisValue)"run-1",
            CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task PublishRunFinished_RemovesFromActiveSet_AppendsAndTrimsRecentList()
    {
        // 2026-10-02-5ab2c: the three index commands are one transaction.
        var tx = new Mock<ITransaction>();
        tx.Setup(t => t.ExecuteAsync(It.IsAny<CommandFlags>())).ReturnsAsync(true);
        _db.Setup(d => d.CreateTransaction(It.IsAny<object?>())).Returns(tx.Object);
        var sut = new RedisEventPublisher(_redis.Object, new AgentSmith.Infrastructure.Services.Events.EventEnvelopeSerializer(), NullLogger<RedisEventPublisher>.Instance);
        var ev = new RunFinishedEvent("run-2", "success", null, "done", DateTimeOffset.UtcNow);

        await sut.PublishAsync(ev);

        tx.Verify(d => d.SetRemoveAsync(
            (RedisKey)EventStreamKeys.ActiveRunsSet, (RedisValue)"run-2",
            CommandFlags.None), Times.Once);
        tx.Verify(d => d.ListLeftPushAsync(
            (RedisKey)EventStreamKeys.RecentRunsList, (RedisValue)"run-2",
            When.Always, CommandFlags.None), Times.Once);
        tx.Verify(d => d.ListTrimAsync(
            (RedisKey)EventStreamKeys.RecentRunsList,
            0, EventStreamKeys.RecentRunsCap - 1, CommandFlags.None), Times.Once);
        tx.Verify(t => t.ExecuteAsync(It.IsAny<CommandFlags>()), Times.Once);
        _db.Verify(d => d.SetRemoveAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    [Fact]
    public async Task PublishAsync_EmptyRunId_Throws()
    {
        var sut = new RedisEventPublisher(_redis.Object, new AgentSmith.Infrastructure.Services.Events.EventEnvelopeSerializer(), NullLogger<RedisEventPublisher>.Instance);
        var ev = new GateCheckedEvent("", "bootstrap", true, "ok", DateTimeOffset.UtcNow);

        var act = async () => await sut.PublishAsync(ev);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*empty RunId*");
    }

    [Fact]
    public async Task PublishAsync_NonLifecycleEvent_DoesNotTouchIndices()
    {
        var sut = new RedisEventPublisher(_redis.Object, new AgentSmith.Infrastructure.Services.Events.EventEnvelopeSerializer(), NullLogger<RedisEventPublisher>.Instance);
        var ev = new StepStartedEvent("run-3", 1, "Triage", 5, DateTimeOffset.UtcNow);

        await sut.PublishAsync(ev);

        _db.Verify(d => d.SetAddAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), CommandFlags.None), Times.Never);
        _db.Verify(d => d.ListLeftPushAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), When.Always, CommandFlags.None), Times.Never);
    }
}
