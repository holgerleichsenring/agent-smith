using AgentSmith.Application.Services.Claim;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Claiming;

/// <summary>
/// 2026-09-25-3c7ab: what a refused lifecycle write may do to a claim the database already
/// granted. p0262 made the claim lease-only and the unique index the sole guard; one branch was
/// left behind, handing a held lease back because a label could not be written.
/// <para>
/// A concurrency refusal means somebody else wrote to the ticket in the same second — an operator
/// typing a comment is enough. A ticket that is NOT THERE, and a tracker that cannot be written to
/// at all, still fail: the board may not decide what the work IS, but it may still say that it is
/// unreachable.
/// </para>
/// </summary>
public sealed class BoardRefusalKeepsTheClaimTests
{
    private readonly Mock<IActiveRunLease> _lease = new();
    private readonly Mock<IRedisJobQueue> _queue = new();
    private readonly Mock<ITicketStatusTransitioner> _transitioner = new();

    public BoardRefusalKeepsTheClaimTests() =>
        _lease.Setup(l => l.TryClaimAsync(
                It.IsAny<string>(), It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LeaseClaimOutcome.Claimed);

    [Fact]
    public async Task Claim_APreconditionFailedLifecycleWrite_KeepsTheLeaseAndProceeds()
    {
        Answering(TransitionResult.PreconditionFailed("etag mismatch"));

        var result = await ExecuteAsync();

        result.Outcome.Should().Be(ClaimOutcome.Claimed,
            "the index granted this claim and a label that would not be written does not take it away");
        _queue.Verify(
            q => q.EnqueueAsync(It.IsAny<PipelineRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        _lease.Verify(
            l => l.ReleaseAsync(
                It.IsAny<string>(), It.IsAny<TicketId>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Claim_APreconditionFailedLifecycleWrite_IsNotReportedAsAlreadyClaimed()
    {
        Answering(TransitionResult.PreconditionFailed("rev mismatch"));

        var result = await ExecuteAsync();

        result.Outcome.Should().NotBe(ClaimOutcome.AlreadyClaimed,
            "nobody else holds this ticket — saying so was a lie about a claim the unique index "
            + "had just granted");
    }

    [Fact]
    public async Task Claim_ATicketThatDoesNotExist_StillFailsTheClaimAndReleasesTheLease()
    {
        Answering(TransitionResult.NotFound());

        var result = await ExecuteAsync();

        result.Outcome.Should().Be(ClaimOutcome.Failed,
            "a 404 is not a missing label, it is a missing ticket, and a run against one produces "
            + "work nobody can deliver");
        _lease.Verify(
            l => l.ReleaseAsync(It.IsAny<string>(), It.IsAny<TicketId>(), null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Claim_ATrackerThatCannotBeWrittenTo_StillFailsTheClaim()
    {
        Answering(TransitionResult.Failed("gateway timeout"));

        var result = await ExecuteAsync();

        result.Outcome.Should().Be(ClaimOutcome.Failed,
            "a run that cannot report its state at the end is worse than one that does not start");
    }

    [Fact]
    public async Task Claim_AWritableBoard_BehavesExactlyAsBefore()
    {
        Answering(TransitionResult.Succeeded());

        var result = await ExecuteAsync();

        result.Outcome.Should().Be(ClaimOutcome.Claimed);
        _queue.Verify(
            q => q.EnqueueAsync(It.IsAny<PipelineRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private void Answering(TransitionResult result) =>
        _transitioner.Setup(t => t.TransitionAsync(
                It.IsAny<TicketId>(), It.IsAny<TicketLifecycleStatus>(),
                It.IsAny<TicketLifecycleStatus>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    private Task<ClaimResult> ExecuteAsync()
    {
        var factory = new Mock<ITicketStatusTransitionerFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(_transitioner.Object);
        var sut = new SingleClaimRegionExecutor(
            factory.Object, _queue.Object, _lease.Object, NullLogger.Instance);
        return sut.ExecuteAsync(
            new ClaimRequest("jira", "sample", new TicketId("1"), "code"),
            new TrackerConnection { Name = "tr", Type = TrackerType.Jira },
            CancellationToken.None);
    }
}

/// <summary>
/// 2026-09-25-3c7ab: the other end of the run. A lifecycle write the tracker refuses at run end is
/// logged and nothing else — the run's own result is untouched, and no standing fact is recorded
/// against the ticket.
/// </summary>
public sealed class RefusedRunEndWriteTests
{
    [Fact]
    public async Task Lifecycle_ARefusedWriteAtRunEnd_DoesNotFailTheRun()
    {
        var transitioner = new Mock<ITicketStatusTransitioner>();
        transitioner.Setup(t => t.TransitionAsync(
                It.IsAny<TicketId>(), It.IsAny<TicketLifecycleStatus>(),
                It.IsAny<TicketLifecycleStatus>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TransitionResult.Failed("the board said no"));
        var factory = new Mock<ITicketStatusTransitionerFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(transitioner.Object);

        var act = async () =>
        {
            var scope = await new AgentSmith.Application.Services.Lifecycle
                .TicketAwarePipelineLifecycleCoordinator(
                    factory.Object,
                    NullLogger<AgentSmith.Application.Services.Lifecycle
                        .TicketAwarePipelineLifecycleCoordinator>.Instance)
                .BeginAsync(
                    new ResolvedProject
                    {
                        Name = "sample",
                        Tracker = new TrackerConnection { Name = "tr", Type = TrackerType.Jira },
                    },
                    Context(), CancellationToken.None);
            await scope.DisposeAsync();
        };

        await act.Should().NotThrowAsync(
            "a label the tracker would not write is cosmetic; the work it describes is done");
    }

    private static PipelineContext Context()
    {
        var context = new PipelineContext();
        context.Set(ContextKeys.TicketId, new TicketId("1"));
        return context;
    }
}
