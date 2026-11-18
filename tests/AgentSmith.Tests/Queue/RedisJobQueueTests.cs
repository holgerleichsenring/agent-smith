using System.Text.Json;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Services.Queue;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace AgentSmith.Tests.Queue;

/// <summary>2026-11-18-550aa: what the queue hands back is what the launcher sent.</summary>
public sealed class RedisJobQueueTests
{
    [Fact]
    public async Task ConsumeAsync_SeriesBeatRequest_HandsBackTheSeriesIdAsAString()
    {
        // The request SeriesRunLaunch dispatches, as RedisJobQueue.EnqueueAsync stores it.
        var sent = new PipelineRequest("p1", "series-run", TicketId: null, Headless: true, RunId: "r1",
            Context: new Dictionary<string, object> { [ContextKeys.SeriesRunId] = "sr-1" });
        var database = new Mock<IDatabase>();
        database.Setup(d => d.ListLeftPopAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)JsonSerializer.Serialize(sent));
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(database.Object);
        var queue = new RedisJobQueue(redis.Object, NullLogger<RedisJobQueue>.Instance);

        PipelineRequest? popped = null;
        await foreach (var request in queue.ConsumeAsync(CancellationToken.None)) { popped = request; break; }

        var pipeline = new PipelineContext();
        foreach (var (key, value) in popped!.Context!) pipeline.Set(key, value);
        pipeline.TryGet<string>(ContextKeys.SeriesRunId, out var series).Should().BeTrue("ReadSeriesHandler reads it so");
        series.Should().Be("sr-1");
    }
}
