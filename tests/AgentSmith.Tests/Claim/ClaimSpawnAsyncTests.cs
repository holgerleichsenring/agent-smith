using AgentSmith.Application.Services.Claim;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Claim;

/// <summary>
/// p0140b: <see cref="TicketClaimService.ClaimSpawnAsync"/> is the claim-region for multi-repo
/// spawn. One pre-check + one lock + one lifecycle transition + N enqueues. These tests
/// pin the dedup guarantee (lock and transition both fire exactly once even when N requests
/// share a ticket).
/// </summary>
public sealed class ClaimSpawnAsyncTests
{
    private const string LockToken = "lock-token";

    [Fact]
    public async Task ClaimSpawn_OneRequest_PreCheckPasses_LocksOnce_TransitionsOnce_EnqueuesOnce()
    {
        var (sut, h) = BuildHarness();
        h.SetupLockAcquired().SetupReadCurrent(null).SetupTransition(TransitionOutcome.Succeeded);

        var results = await sut.ClaimSpawnAsync(
            new[] { Request("repo-a") }, Config(), CancellationToken.None);

        results.Should().HaveCount(1);
        results[0].Outcome.Should().Be(ClaimOutcome.Claimed);
        h.ClaimLock.Verify(l => l.TryAcquireAsync(
            It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
        h.Transitioner.Verify(t => t.TransitionAsync(
            It.IsAny<TicketId>(), TicketLifecycleStatus.Pending, TicketLifecycleStatus.Enqueued,
            It.IsAny<CancellationToken>()), Times.Once);
        h.JobQueue.Verify(q => q.EnqueueAsync(
            It.IsAny<PipelineRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClaimSpawn_ThreeRequests_SharedTicket_AllEnqueueUnderOneLock_OneTransition()
    {
        var (sut, h) = BuildHarness();
        h.SetupLockAcquired().SetupReadCurrent(null).SetupTransition(TransitionOutcome.Succeeded);

        var results = await sut.ClaimSpawnAsync(
            new[] { Request("repo-a"), Request("repo-b"), Request("repo-c") },
            Config(), CancellationToken.None);

        results.Should().HaveCount(3);
        results.Should().AllSatisfy(r => r.Outcome.Should().Be(ClaimOutcome.Claimed));
        h.ClaimLock.Verify(l => l.TryAcquireAsync(
            It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
        h.Transitioner.Verify(t => t.TransitionAsync(
            It.IsAny<TicketId>(), TicketLifecycleStatus.Pending, TicketLifecycleStatus.Enqueued,
            It.IsAny<CancellationToken>()), Times.Once);
        h.JobQueue.Verify(q => q.EnqueueAsync(
            It.IsAny<PipelineRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        h.JobQueue.Verify(q => q.EnqueueAsync(
            It.Is<PipelineRequest>(r => r.RepoName == "repo-a"), It.IsAny<CancellationToken>()), Times.Once);
        h.JobQueue.Verify(q => q.EnqueueAsync(
            It.Is<PipelineRequest>(r => r.RepoName == "repo-b"), It.IsAny<CancellationToken>()), Times.Once);
        h.JobQueue.Verify(q => q.EnqueueAsync(
            It.Is<PipelineRequest>(r => r.RepoName == "repo-c"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClaimSpawn_AnyPreCheckFails_AllRejected_NoEnqueue()
    {
        var (sut, h) = BuildHarness();
        // No need to set up lock/transition because pre-check fails before that.
        var requests = new[]
        {
            Request("repo-a"),
            Request("repo-b") with { ProjectName = "unknown-project" } // pre-check rejects this one
        };

        var results = await sut.ClaimSpawnAsync(requests, Config(), CancellationToken.None);

        results.Should().HaveCount(2);
        results.Should().AllSatisfy(r => r.Outcome.Should().Be(ClaimOutcome.Rejected));
        results.Should().AllSatisfy(r => r.Rejection.Should().Be(ClaimRejectionReason.UnknownProject));
        h.ClaimLock.Verify(l => l.TryAcquireAsync(
            It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
        h.JobQueue.Verify(q => q.EnqueueAsync(
            It.IsAny<PipelineRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ClaimSpawn_TicketLifecycleNotPending_AllAlreadyClaimed_NoEnqueue()
    {
        var (sut, h) = BuildHarness();
        h.SetupLockAcquired().SetupReadCurrent(TicketLifecycleStatus.Enqueued);

        var results = await sut.ClaimSpawnAsync(
            new[] { Request("repo-a"), Request("repo-b") }, Config(), CancellationToken.None);

        results.Should().HaveCount(2);
        results.Should().AllSatisfy(r => r.Outcome.Should().Be(ClaimOutcome.AlreadyClaimed));
        h.Transitioner.Verify(t => t.TransitionAsync(
            It.IsAny<TicketId>(), It.IsAny<TicketLifecycleStatus>(), It.IsAny<TicketLifecycleStatus>(),
            It.IsAny<CancellationToken>()), Times.Never);
        h.JobQueue.Verify(q => q.EnqueueAsync(
            It.IsAny<PipelineRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ClaimAsync_SingleRequest_DelegatesToClaimSpawn()
    {
        var (sut, h) = BuildHarness();
        h.SetupLockAcquired().SetupReadCurrent(null).SetupTransition(TransitionOutcome.Succeeded);

        var result = await sut.ClaimAsync(Request("repo-a"), Config(), CancellationToken.None);

        result.Outcome.Should().Be(ClaimOutcome.Claimed);
        h.JobQueue.Verify(q => q.EnqueueAsync(
            It.IsAny<PipelineRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        h.ClaimLock.Verify(l => l.TryAcquireAsync(
            It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static (TicketClaimService sut, Harness h) BuildHarness()
    {
        var h = new Harness();
        var sut = new TicketClaimService(
            h.ClaimLock.Object, h.Factory.Object, h.JobQueue.Object,
            NullLogger<TicketClaimService>.Instance);
        return (sut, h);
    }

    private static ClaimRequest Request(string repoName)
        => new("GitHub", "my-project", new TicketId("42"), "fix-bug", RepoName: repoName);

    private static AgentSmithConfig Config() => new()
    {
        Projects = new()
        {
            ["my-project"] = new ResolvedProject
            {
                Name = "my-project",
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
            Factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(Transitioner.Object);
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
                It.IsAny<TicketId>(), It.IsAny<TicketLifecycleStatus>(),
                It.IsAny<TicketLifecycleStatus>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TransitionResult(outcome, error));
            return this;
        }
    }
}
