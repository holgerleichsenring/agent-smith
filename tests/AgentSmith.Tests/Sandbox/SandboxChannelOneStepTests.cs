using System.Collections.Concurrent;
using System.Text.Json;
using AgentSmith.Server.Services.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-10-13-3890d: an agent answers one step at a time onto one result list. Two callers of the
/// same channel must each get their own result — before, the second waiter popped the first
/// one's result and threw it away, and the first timed out.
/// </summary>
public sealed class SandboxChannelOneStepTests
{
    [Fact]
    public async Task Channel_TwoConcurrentSteps_EachGetsItsOwnResult()
    {
        var agent = new OneAtATimeAgent();
        var channel = agent.Channel();
        var first = NewStep();
        var second = NewStep();

        var both = Task.WhenAll(
            channel.RunStepAsync(first, null, TimeSpan.FromMinutes(5), CancellationToken.None),
            channel.RunStepAsync(second, null, TimeSpan.FromMinutes(5), CancellationToken.None));
        await agent.AnswerNextAsync();
        await agent.AnswerNextAsync();
        var results = await both;

        results.Select(r => r.StepId).Should().BeEquivalentTo([first.StepId, second.StepId]);
        agent.Discarded.Should().Be(0, "a result is only ever popped by the caller whose step it is");
        agent.MostPending.Should().Be(1, "the second step is pushed only once the first one's result is taken");
    }

    private static Step NewStep() =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run, Command: "true", TimeoutSeconds: 30);

    /// <summary>Takes pushed steps in order and answers one when the test says so, onto one shared list.</summary>
    private sealed class OneAtATimeAgent
    {
        private readonly System.Threading.Channels.Channel<Guid> _pushed = System.Threading.Channels.Channel.CreateUnbounded<Guid>();
        private readonly ConcurrentQueue<RedisValue> _results = new();
        private int _discarded;
        private int _pending;

        public int Discarded => _discarded;

        public int MostPending { get; private set; }

        public async Task AnswerNextAsync()
        {
            var stepId = await _pushed.Reader.ReadAsync();
            Interlocked.Decrement(ref _pending);
            _results.Enqueue(JsonSerializer.Serialize(
                new StepResult(StepResult.CurrentSchemaVersion, stepId, 0, false, 0.1, null, "ok"), WireFormat.Json));
        }

        public SandboxRedisChannel Channel()
        {
            var db = new Mock<IDatabase>();
            db.Setup(d => d.ListLeftPushAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
                .Returns((RedisKey _, RedisValue json, When _, CommandFlags _) =>
                {
                    var step = JsonSerializer.Deserialize<Step>((string)json!, WireFormat.Json)!;
                    MostPending = Math.Max(MostPending, Interlocked.Increment(ref _pending));
                    _pushed.Writer.TryWrite(step.StepId);
                    return Task.FromResult(1L);
                });
            db.Setup(d => d.ListRightPopAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(() => _results.TryDequeue(out var r) ? r : RedisValue.Null);
            db.Setup(d => d.StreamReadAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<int?>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync([]);
            db.Setup(d => d.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);
            var multiplexer = new Mock<IConnectionMultiplexer>();
            multiplexer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(db.Object);
            var logger = new Mock<Microsoft.Extensions.Logging.ILogger>();
            logger.Setup(l => l.Log(Microsoft.Extensions.Logging.LogLevel.Warning, It.IsAny<Microsoft.Extensions.Logging.EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Discarded stale")), It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
                .Callback(() => Interlocked.Increment(ref _discarded));
            return new SandboxRedisChannel(multiplexer.Object, "job-3890d", logger.Object, Mock.Of<IWireProtocolWatcher>());
        }
    }
}
