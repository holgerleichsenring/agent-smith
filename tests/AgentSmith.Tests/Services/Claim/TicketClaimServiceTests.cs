using AgentSmith.Application.Services.Claim;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services.Claim;

public sealed class TicketClaimServiceTests
{
    private const string LockToken = "abcdef";

    [Fact]
    public async Task ClaimAsync_PendingTicket_ReturnsClaimedAndEnqueues()
    {
        var (sut, harness) = BuildHarness();
        harness.SetupLockAcquired().SetupReadCurrent(null).SetupTransition(TransitionOutcome.Succeeded);

        var result = await sut.ClaimAsync(ValidRequest(), ValidConfig(), CancellationToken.None);

        result.Outcome.Should().Be(ClaimOutcome.Claimed);
        harness.JobQueue.Verify(q => q.EnqueueAsync(
            It.Is<PipelineRequest>(r => r.ProjectName == "my-project"), It.IsAny<CancellationToken>()), Times.Once);
        harness.ClaimLock.Verify(l => l.ReleaseAsync(
            It.IsAny<string>(), LockToken, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClaimAsync_AlreadyEnqueuedTicket_ReturnsAlreadyClaimed()
    {
        var (sut, harness) = BuildHarness();
        harness.SetupLockAcquired().SetupReadCurrent(TicketLifecycleStatus.Enqueued);

        var result = await sut.ClaimAsync(ValidRequest(), ValidConfig(), CancellationToken.None);

        result.Outcome.Should().Be(ClaimOutcome.AlreadyClaimed);
        harness.JobQueue.Verify(q => q.EnqueueAsync(
            It.IsAny<PipelineRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ClaimAsync_LockAlreadyHeld_ReturnsAlreadyClaimed()
    {
        var (sut, harness) = BuildHarness();
        harness.ClaimLock
            .Setup(l => l.TryAcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await sut.ClaimAsync(ValidRequest(), ValidConfig(), CancellationToken.None);

        result.Outcome.Should().Be(ClaimOutcome.AlreadyClaimed);
    }

    [Fact]
    public async Task ClaimAsync_UnknownProject_ReturnsRejected_WithoutTakingLock()
    {
        var (sut, harness) = BuildHarness();
        var request = ValidRequest() with { ProjectName = "unknown" };

        var result = await sut.ClaimAsync(request, ValidConfig(), CancellationToken.None);

        result.Outcome.Should().Be(ClaimOutcome.Rejected);
        result.Rejection.Should().Be(ClaimRejectionReason.UnknownProject);
        harness.ClaimLock.Verify(l => l.TryAcquireAsync(
            It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ClaimAsync_StatusTransitionPreconditionFailed_ReturnsAlreadyClaimed()
    {
        var (sut, harness) = BuildHarness();
        harness.SetupLockAcquired().SetupReadCurrent(null)
            .SetupTransition(TransitionOutcome.PreconditionFailed);

        var result = await sut.ClaimAsync(ValidRequest(), ValidConfig(), CancellationToken.None);

        result.Outcome.Should().Be(ClaimOutcome.AlreadyClaimed);
        harness.JobQueue.Verify(q => q.EnqueueAsync(
            It.IsAny<PipelineRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ClaimAsync_StatusTransitionFails_ReturnsFailed_AndDoesNotEnqueue()
    {
        var (sut, harness) = BuildHarness();
        harness.SetupLockAcquired().SetupReadCurrent(null)
            .SetupTransition(TransitionOutcome.Failed, "upstream 500");

        var result = await sut.ClaimAsync(ValidRequest(), ValidConfig(), CancellationToken.None);

        result.Outcome.Should().Be(ClaimOutcome.Failed);
        result.Error.Should().Contain("upstream 500");
        harness.JobQueue.Verify(q => q.EnqueueAsync(
            It.IsAny<PipelineRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.ClaimLock.Verify(l => l.ReleaseAsync(
            It.IsAny<string>(), LockToken, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClaimAsync_EnqueueFails_ReturnsFailed_StatusStaysEnqueued_LockReleased()
    {
        var (sut, harness) = BuildHarness();
        harness.SetupLockAcquired().SetupReadCurrent(null)
            .SetupTransition(TransitionOutcome.Succeeded);
        harness.JobQueue
            .Setup(q => q.EnqueueAsync(It.IsAny<PipelineRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("redis down"));

        var result = await sut.ClaimAsync(ValidRequest(), ValidConfig(), CancellationToken.None);

        result.Outcome.Should().Be(ClaimOutcome.Failed);
        result.Error.Should().Contain("redis down");
        harness.ClaimLock.Verify(l => l.ReleaseAsync(
            It.IsAny<string>(), LockToken, It.IsAny<CancellationToken>()), Times.Once);
        // Status stays Enqueued (we verify no rollback attempt)
        harness.Transitioner.Verify(t => t.TransitionAsync(
            It.IsAny<TicketId>(),
            TicketLifecycleStatus.Enqueued,
            TicketLifecycleStatus.Pending,
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (TicketClaimService sut, Harness h) BuildHarness()
    {
        var h = new Harness();
        var sut = new TicketClaimService(
            h.ClaimLock.Object, h.Factory.Object, h.JobQueue.Object,
            NullLogger<TicketClaimService>.Instance);
        return (sut, h);
    }

    private static ClaimRequest ValidRequest()
        => new("GitHub", "my-project", new TicketId("42"), "fix-bug");

    private static AgentSmithConfig ValidConfig() => new()
    {
        Projects = new()
        {
            ["my-project"] = new ProjectConfig
            {
                GithubTrigger = new WebhookTriggerConfig { DefaultPipeline = "fix-bug" }
            }
        }
    };

    private sealed class Harness
    {
        public Mock<IRedisClaimLock> ClaimLock { get; } = new();
        public Mock<ITicketStatusTransitionerFactory> Factory { get; } = new();
        public Mock<ITicketStatusTransitioner> Transitioner { get; } = new();
        public Mock<IRedisJobQueue> JobQueue { get; } = new();

        public Harness()
        {
            Factory.Setup(f => f.Create(It.IsAny<TicketConfig>())).Returns(Transitioner.Object);
            ClaimLock.Setup(l => l.ReleaseAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        public Harness SetupLockAcquired()
        {
            ClaimLock.Setup(l => l.TryAcquireAsync(
                It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(LockToken);
            return this;
        }

        public Harness SetupReadCurrent(TicketLifecycleStatus? current)
        {
            Transitioner.Setup(t => t.ReadCurrentAsync(
                It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(current);
            return this;
        }

        public Harness SetupTransition(TransitionOutcome outcome, string? error = null)
        {
            Transitioner.Setup(t => t.TransitionAsync(
                It.IsAny<TicketId>(),
                It.IsAny<TicketLifecycleStatus>(),
                It.IsAny<TicketLifecycleStatus>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TransitionResult(outcome, error));
            return this;
        }
    }
}
