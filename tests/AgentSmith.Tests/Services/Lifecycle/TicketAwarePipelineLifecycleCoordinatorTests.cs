using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services.Lifecycle;

public sealed class TicketAwarePipelineLifecycleCoordinatorTests
{
    [Fact]
    public async Task BeginAsync_NoTicketId_ReturnsNoopScope_NoCalls()
    {
        var (factory, transitioner, heartbeat) = MockServerSide();
        var sut = Sut(factory, heartbeat);

        var scope = await sut.BeginAsync(new ProjectConfig(), new PipelineContext(), CancellationToken.None);

        await scope.DisposeAsync();
        transitioner.VerifyNoOtherCalls();
        heartbeat.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task BeginAsync_WithTicketId_TransitionsEnqueuedToInProgress_StartsHeartbeat()
    {
        var (factory, transitioner, heartbeat) = MockServerSide();
        var sut = Sut(factory, heartbeat);

        var scope = await sut.BeginAsync(new ProjectConfig(), TicketContext("PROJ-1"), CancellationToken.None);

        transitioner.Verify(t => t.TransitionAsync(
            It.Is<TicketId>(id => id.Value == "PROJ-1"),
            TicketLifecycleStatus.Enqueued, TicketLifecycleStatus.InProgress,
            It.IsAny<CancellationToken>()), Times.Once);
        heartbeat.Verify(h => h.Start(It.Is<TicketId>(id => id.Value == "PROJ-1")), Times.Once);
        await scope.DisposeAsync();
    }

    [Fact]
    public async Task DisposeWithoutFailure_TransitionsInProgressToDone()
    {
        var (factory, transitioner, heartbeat) = MockServerSide();
        var sut = Sut(factory, heartbeat);

        var scope = await sut.BeginAsync(new ProjectConfig(), TicketContext("PROJ-1"), CancellationToken.None);
        await scope.DisposeAsync();

        transitioner.Verify(t => t.TransitionAsync(
            It.IsAny<TicketId>(),
            TicketLifecycleStatus.InProgress, TicketLifecycleStatus.Done,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkFailedThenDispose_TransitionsToFailed()
    {
        var (factory, transitioner, heartbeat) = MockServerSide();
        var sut = Sut(factory, heartbeat);

        var scope = await sut.BeginAsync(new ProjectConfig(), TicketContext("PROJ-1"), CancellationToken.None);
        scope.MarkFailed();
        await scope.DisposeAsync();

        transitioner.Verify(t => t.TransitionAsync(
            It.IsAny<TicketId>(),
            TicketLifecycleStatus.InProgress, TicketLifecycleStatus.Failed,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BeginAsync_TransitionerThrows_FallsBackToNoopScope()
    {
        var factory = new Mock<ITicketStatusTransitionerFactory>();
        factory.Setup(f => f.Create(It.IsAny<TicketConfig>()))
            .Throws(new InvalidOperationException("transient"));
        var heartbeat = new Mock<IJobHeartbeatService>();
        var sut = Sut(factory, heartbeat);

        var scope = await sut.BeginAsync(new ProjectConfig(), TicketContext("PROJ-1"), CancellationToken.None);

        var act = async () => await scope.DisposeAsync();
        await act.Should().NotThrowAsync();
        heartbeat.Verify(h => h.Start(It.IsAny<TicketId>()), Times.Never);
    }

    private static (Mock<ITicketStatusTransitionerFactory>, Mock<ITicketStatusTransitioner>, Mock<IJobHeartbeatService>)
        MockServerSide()
    {
        var transitioner = new Mock<ITicketStatusTransitioner>();
        transitioner.Setup(t => t.TransitionAsync(
                It.IsAny<TicketId>(), It.IsAny<TicketLifecycleStatus>(),
                It.IsAny<TicketLifecycleStatus>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TransitionResult.Succeeded());
        var factory = new Mock<ITicketStatusTransitionerFactory>();
        factory.Setup(f => f.Create(It.IsAny<TicketConfig>())).Returns(transitioner.Object);
        var heartbeat = new Mock<IJobHeartbeatService>();
        heartbeat.Setup(h => h.Start(It.IsAny<TicketId>())).Returns(Mock.Of<IAsyncDisposable>());
        return (factory, transitioner, heartbeat);
    }

    private static TicketAwarePipelineLifecycleCoordinator Sut(
        Mock<ITicketStatusTransitionerFactory> factory, Mock<IJobHeartbeatService> heartbeat)
        => new(factory.Object, heartbeat.Object,
            NullLogger<TicketAwarePipelineLifecycleCoordinator>.Instance);

    private static PipelineContext TicketContext(string id)
    {
        var ctx = new PipelineContext();
        ctx.Set(ContextKeys.TicketId, new TicketId(id));
        return ctx;
    }
}
