using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Services;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Server.Services.Sandbox;
using AgentSmith.Tests.TestHelpers;
using Docker.DotNet;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-10-13-9a59i: the coordinator hands the supervisor its projector, not the backend. The supervisor
/// unwraps to the Docker sandbox behind it to find the container to watch — the projector no longer
/// forwards the probe id.
/// </summary>
public sealed class SandboxLivenessSupervisorTests
{
    [Fact]
    public async Task SandboxLivenessSupervisor_WrappedDockerSandbox_StartsWatcher()
    {
        var watched = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var database = new Mock<IDatabase>();
        database.Setup(d => d.KeyExistsAsync(RedisKeys.HeartbeatKey("job-1"), It.IsAny<CommandFlags>()))
            .Callback(() => watched.TrySetResult())
            .ReturnsAsync(true);
        var multiplexer = new Mock<IConnectionMultiplexer>();
        multiplexer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(database.Object);
        var docker = Mock.Of<IDockerClient>();
        var backend = new DockerSandbox(docker, "toolchain-1", "shared", "work", "job-1",
            new SandboxRedisChannel(multiplexer.Object, "job-1", NullLogger.Instance, Mock.Of<IWireProtocolWatcher>()),
            stepTimeoutCapSeconds: 900, "node:20", NullLogger.Instance);
        var wrapped = new SandboxEventProjector(backend, EventTestStubs.NoOp, EventTestStubs.RunContext, "api");
        await using var supervisor = new SandboxLivenessSupervisor(
            multiplexer.Object, docker, Mock.Of<IRunCancellationRegistry>(), EventTestStubs.NoOp, NullLoggerFactory.Instance);

        supervisor.Watch("run-1", "api", wrapped);

        await watched.Task.OrHang("the watcher polls the wrapped sandbox's heartbeat");
        watched.Task.IsCompletedSuccessfully.Should().BeTrue();
    }
}
