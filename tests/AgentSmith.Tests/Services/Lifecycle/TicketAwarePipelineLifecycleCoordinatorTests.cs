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
        var (factory, transitioner) = MockServerSide();
        var sut = Sut(factory);

        var scope = await sut.BeginAsync(new ResolvedProject(), new PipelineContext(), CancellationToken.None);

        await scope.DisposeAsync();
        transitioner.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task BeginAsync_WithTicketId_TransitionsEnqueuedToInProgress()
    {
        var (factory, transitioner) = MockServerSide();
        var sut = Sut(factory);

        var scope = await sut.BeginAsync(new ResolvedProject(), TicketContext("PROJ-1"), CancellationToken.None);

        transitioner.Verify(t => t.TransitionAsync(
            It.Is<TicketId>(id => id.Value == "PROJ-1"),
            TicketLifecycleStatus.Enqueued, TicketLifecycleStatus.InProgress,
            It.IsAny<CancellationToken>()), Times.Once);
        await scope.DisposeAsync();
    }

    [Fact]
    public async Task DisposeWithoutFailure_TransitionsInProgressToDone()
    {
        var (factory, transitioner) = MockServerSide();
        var sut = Sut(factory);

        var scope = await sut.BeginAsync(new ResolvedProject(), TicketContext("PROJ-1"), CancellationToken.None);
        await scope.DisposeAsync();

        transitioner.Verify(t => t.TransitionAsync(
            It.IsAny<TicketId>(),
            TicketLifecycleStatus.InProgress, TicketLifecycleStatus.Done,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkFailedThenDispose_TransitionsToFailed()
    {
        var (factory, transitioner) = MockServerSide();
        var sut = Sut(factory);

        var scope = await sut.BeginAsync(new ResolvedProject(), TicketContext("PROJ-1"), CancellationToken.None);
        scope.MarkFailed();
        await scope.DisposeAsync();

        transitioner.Verify(t => t.TransitionAsync(
            It.IsAny<TicketId>(),
            TicketLifecycleStatus.InProgress, TicketLifecycleStatus.Failed,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DisposeAfterCompletedReRun_ClearsStalePriorFailedTag()
    {
        // p0262: the run-end tag is written UNCONDITIONALLY (a pure marker) — the
        // coordinator no longer reads the current lifecycle to anchor `from`. A re-run
        // of a ticket a previous run left as Failed still ends Done: the platform
        // transitioner strips any stale lifecycle tag and writes Done regardless. The
        // advisory `from` is InProgress.
        var (factory, transitioner) = MockServerSide();
        var sut = Sut(factory);

        var scope = await sut.BeginAsync(new ResolvedProject(), TicketContext("PROJ-1"), CancellationToken.None);
        await scope.DisposeAsync();

        transitioner.Verify(t => t.TransitionAsync(
            It.IsAny<TicketId>(),
            TicketLifecycleStatus.InProgress, TicketLifecycleStatus.Done,
            It.IsAny<CancellationToken>()), Times.Once);
        transitioner.Verify(t => t.ReadCurrentAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BeginAsync_TransitionerThrows_FallsBackToNoopScope()
    {
        var factory = new Mock<ITicketStatusTransitionerFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>()))
            .Throws(new InvalidOperationException("transient"));
        var sut = Sut(factory);

        var scope = await sut.BeginAsync(new ResolvedProject(), TicketContext("PROJ-1"), CancellationToken.None);

        var act = async () => await scope.DisposeAsync();
        await act.Should().NotThrowAsync();
    }

    private static (Mock<ITicketStatusTransitionerFactory>, Mock<ITicketStatusTransitioner>)
        MockServerSide()
    {
        var transitioner = new Mock<ITicketStatusTransitioner>();
        transitioner.Setup(t => t.TransitionAsync(
                It.IsAny<TicketId>(), It.IsAny<TicketLifecycleStatus>(),
                It.IsAny<TicketLifecycleStatus>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TransitionResult.Succeeded());
        // p0237: the terminal write reads current first; default the happy path
        // to InProgress so the existing dispose assertions hold.
        transitioner.Setup(t => t.ReadCurrentAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TicketLifecycleStatus.InProgress);
        var factory = new Mock<ITicketStatusTransitionerFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(transitioner.Object);
        return (factory, transitioner);
    }

    private static TicketAwarePipelineLifecycleCoordinator Sut(
        Mock<ITicketStatusTransitionerFactory> factory)
        => new(factory.Object,
            NullLogger<TicketAwarePipelineLifecycleCoordinator>.Instance);

    private static PipelineContext TicketContext(string id)
    {
        var ctx = new PipelineContext();
        ctx.Set(ContextKeys.TicketId, new TicketId(id));
        return ctx;
    }
}
