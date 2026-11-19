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
/// 2026-08-24-035a: the host binds after the broadcaster's StartAsync returns, and a read against a
/// multiplexer with no connection waits out its backlog timeout before it fails. A disconnected Redis
/// therefore skips the cold start instead of holding the port shut for the same empty start.
/// </summary>
public sealed class JobsBroadcasterColdStartTests
{
    [Fact]
    public async Task Start_RedisNotConnected_SkipsTheReads()
    {
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(r => r.IsConnected).Returns(false);
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(Mock.Of<IDatabase>());
        var log = new RecordingLogger();
        var broadcaster = new JobsBroadcaster(redis.Object, Mock.Of<IRunEventFanout>(), NewRouter(),
            log, new AgentSmith.Infrastructure.Services.Events.EventEnvelopeSerializer());

        await broadcaster.StartAsync(CancellationToken.None);
        await broadcaster.StopAsync(CancellationToken.None);

        log.Messages.Should().Contain(m => m.Contains("cold start skipped"));
        log.Messages.Should().NotContain(m => m.Contains("could not read Redis"),
            "no read was attempted, so none waited out the backlog and failed");
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
