using AgentSmith.Tests.TestHelpers;
using AgentSmith.Server.Hubs;
using AgentSmith.Server.Services.Events;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Infrastructure.Persistence.Services;
using Microsoft.AspNetCore.SignalR;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-08-24-035a: a read against a multiplexer with no connection waits out its backlog timeout before it fails.
/// 2026-11-22-747db: StartAsync only starts the broadcaster; its cold start waits for the connection, after the host
/// has bound its port, instead of reading and failing — or holding the port shut — before it.
/// </summary>
public sealed class JobsBroadcasterColdStartTests
{
    [Fact]
    public async Task Start_RedisNotConnected_ReturnsAtOnceAndWaitsForTheConnection()
    {
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(r => r.IsConnected).Returns(false);
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(Mock.Of<IDatabase>());
        var log = new RecordingLogger();
        var broadcaster = new JobsBroadcaster(redis.Object, Mock.Of<IRunEventFanout>(), NewRouter(),
            log, new AgentSmith.Infrastructure.Services.Events.EventEnvelopeSerializer());

        await broadcaster.StartAsync(CancellationToken.None).OrHang("a StartAsync that only starts the loop");
        var waits = await TestWaits.ReachedAsync(() => log.Messages.Any(m => m.Contains("waits for Redis")));
        await broadcaster.StopAsync(CancellationToken.None);

        waits.Should().BeTrue();
        broadcaster.Warm.IsCompleted.Should().BeFalse("nothing was read yet");
        log.Messages.Should().NotContain(m => m.Contains("could not read Redis"),
            "no read was attempted, so none waited out the backlog and failed");
    }

    [Fact]
    public async Task Start_ColdStartSlow_StartAsyncReturnsBeforeIt()
    {
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(r => r.IsConnected).Returns(true);
        var db = new Mock<IDatabase>();
        var read = new TaskCompletionSource<RedisValue[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        db.Setup(d => d.SetMembersAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).Returns(read.Task);
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(db.Object);
        var broadcaster = new JobsBroadcaster(redis.Object, Mock.Of<IRunEventFanout>(), NewRouter(),
            new RecordingLogger(), new AgentSmith.Infrastructure.Services.Events.EventEnvelopeSerializer());

        await broadcaster.StartAsync(CancellationToken.None).OrHang("a StartAsync without the cold start");
        var coldBeforeTheRead = broadcaster.Warm.IsCompleted;
        read.SetResult([]);
        var warm = await TestWaits.ReachedAsync(() => broadcaster.Warm.IsCompleted);
        await broadcaster.StopAsync(CancellationToken.None);

        coldBeforeTheRead.Should().BeFalse("StartAsync returned while the cold start's first read was still open");
        warm.Should().BeTrue("it runs once the broadcaster is started");
    }

    private sealed class RecordingLogger : ILogger<JobsBroadcaster>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }

    private static RunEventRouter NewRouter() => new(
        Mock.Of<IRunEventFanout>(), new SandboxExpansionRegistry(),
        new SandboxDetailEventClassifier(), new SandboxActivityCoalescer(),
        new RunDbEventPersistence(null),
        new FiledWorkNudge(Mock.Of<IHubContext<JobsHub>>(), new FiledWorkWatchRegistry()));
}
