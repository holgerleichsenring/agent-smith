using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Providers.Tickets;

public sealed class LockedTicketStatusTransitionerTests
{
    [Fact]
    public async Task TransitionAsync_HappyPath_AcquiresLockCallsInnerReleasesLock()
    {
        var inner = new Mock<ITicketStatusTransitioner>();
        inner.Setup(i => i.TransitionAsync(
                It.IsAny<TicketId>(), It.IsAny<TicketLifecycleStatus>(),
                It.IsAny<TicketLifecycleStatus>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TransitionResult.Succeeded());
        var claimLock = AcquiringLock("tok");
        var sut = new LockedTicketStatusTransitioner(
            inner.Object, claimLock.Object,
            NullLogger<LockedTicketStatusTransitioner>.Instance);

        var result = await sut.TransitionAsync(new TicketId("PROJ-1"),
            TicketLifecycleStatus.Pending, TicketLifecycleStatus.Enqueued, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        claimLock.Verify(l => l.TryAcquireAsync(
            It.Is<string>(k => k == "agentsmith:jira-label-lock:PROJ-1"),
            It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
        inner.Verify(i => i.TransitionAsync(
            It.IsAny<TicketId>(), It.IsAny<TicketLifecycleStatus>(),
            It.IsAny<TicketLifecycleStatus>(), It.IsAny<CancellationToken>()), Times.Once);
        claimLock.Verify(l => l.ReleaseAsync(
            It.Is<string>(k => k == "agentsmith:jira-label-lock:PROJ-1"),
            "tok", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TransitionAsync_LockUnavailable_ReturnsPreconditionFailed_InnerNotCalled()
    {
        var inner = new Mock<ITicketStatusTransitioner>();
        var claimLock = new Mock<IRedisClaimLock>();
        claimLock.Setup(l => l.TryAcquireAsync(
                It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        var sut = new LockedTicketStatusTransitioner(
            inner.Object, claimLock.Object,
            NullLogger<LockedTicketStatusTransitioner>.Instance);

        var result = await sut.TransitionAsync(new TicketId("PROJ-1"),
            TicketLifecycleStatus.Pending, TicketLifecycleStatus.Enqueued, CancellationToken.None);

        result.Outcome.Should().Be(TransitionOutcome.PreconditionFailed);
        inner.Verify(i => i.TransitionAsync(
            It.IsAny<TicketId>(), It.IsAny<TicketLifecycleStatus>(),
            It.IsAny<TicketLifecycleStatus>(), It.IsAny<CancellationToken>()), Times.Never);
        claimLock.Verify(l => l.ReleaseAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TransitionAsync_InnerThrows_LockStillReleased()
    {
        var inner = new Mock<ITicketStatusTransitioner>();
        inner.Setup(i => i.TransitionAsync(
                It.IsAny<TicketId>(), It.IsAny<TicketLifecycleStatus>(),
                It.IsAny<TicketLifecycleStatus>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("kaboom"));
        var claimLock = AcquiringLock("tok");
        var sut = new LockedTicketStatusTransitioner(
            inner.Object, claimLock.Object,
            NullLogger<LockedTicketStatusTransitioner>.Instance);

        var act = () => sut.TransitionAsync(new TicketId("PROJ-1"),
            TicketLifecycleStatus.Pending, TicketLifecycleStatus.Enqueued, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        claimLock.Verify(l => l.ReleaseAsync(
            It.IsAny<string>(), "tok", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReadCurrentAsync_PassesThrough_NoLockInvolved()
    {
        var inner = new Mock<ITicketStatusTransitioner>();
        inner.Setup(i => i.ReadCurrentAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TicketLifecycleStatus.InProgress);
        var claimLock = new Mock<IRedisClaimLock>();
        var sut = new LockedTicketStatusTransitioner(
            inner.Object, claimLock.Object,
            NullLogger<LockedTicketStatusTransitioner>.Instance);

        var status = await sut.ReadCurrentAsync(new TicketId("PROJ-1"), CancellationToken.None);

        status.Should().Be(TicketLifecycleStatus.InProgress);
        claimLock.VerifyNoOtherCalls();
    }

    [Fact]
    public void ProviderType_PassesThroughToInner()
    {
        var inner = new Mock<ITicketStatusTransitioner>();
        inner.SetupGet(i => i.ProviderType).Returns("Jira");
        var sut = new LockedTicketStatusTransitioner(
            inner.Object, Mock.Of<IRedisClaimLock>(),
            NullLogger<LockedTicketStatusTransitioner>.Instance);

        sut.ProviderType.Should().Be("Jira");
    }

    private static Mock<IRedisClaimLock> AcquiringLock(string token)
    {
        var claimLock = new Mock<IRedisClaimLock>();
        claimLock.Setup(l => l.TryAcquireAsync(
                It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        claimLock.Setup(l => l.ReleaseAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return claimLock;
    }
}
