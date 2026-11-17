using AgentSmith.Application.Services.Polling;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Rework.Nudges;

/// <summary>
/// 2026-11-16-f3fbc: a PreconditionsMet nudge through the handler — a pending act is still served as rework; without
/// one the ticket is released once, through the start marked precondition; a ticket a person closed, marked done or
/// one no longer stuck is skipped; a refused release is logged.
/// </summary>
public sealed class PreconditionReleaseTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);
    private readonly ServerStateStore _store = new(configure: ClosedTicketFollowUps.Register);
    private readonly Mock<IPreviousAttemptReader> _attempts = new();
    private readonly Mock<IReworkPendingActs> _pending = new();
    private readonly Mock<IReworkEntry> _rework = new();
    private readonly Mock<IReworkLaunch> _launch = new();
    private readonly Mock<ITicketProvider> _ticket = new();
    private readonly Mock<ITicketProviderFactory> _tickets = new();
    private readonly InMemoryTicketFacts _facts = new();
    private readonly CapturingLogger<PreconditionRelease> _log = new();
    private static readonly ResolvedProject Project = new()
    {
        Name = "p", Tracker = new TrackerConnection { Name = "jira", Type = TrackerType.Jira },
    };
    private static readonly ClaimedReworkNudge Nudge = new("p", "7", ReworkNudgeOrigin.PreconditionsMet, null, null, 1, "tok", 0);
    private static readonly PendingReworkAct Act = new(new ReworkAct("alice", Start.AddMinutes(30)));

    public PreconditionReleaseTests()
    {
        _tickets.Setup(t => t.Create(It.IsAny<TrackerConnection>())).Returns(_ticket.Object);
        Tracker(closed: false);
        Stuck(true);
        _pending.Setup(p => p.NewestAsync(Project, "7", It.IsAny<PreviousAttempt>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PendingReworkAct?)null);
        _launch.Setup(l => l.StartPreconditionReleaseAsync("p", "7", It.IsAny<CancellationToken>())).ReturnsAsync(ReworkOutcome.Started("run-2"));
    }

    public void Dispose() => _store.Dispose();

    private void Stuck(bool stuck) =>
        _attempts.Setup(a => a.LatestAsync("p", "7", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PreviousAttempt("run-1", RunStatuses.Failed, Start, true)
            {
                StoppedOnPrecondition = stuck, EndKind = stuck ? RunEndKinds.ImageUntrusted : null,
            });

    private void Tracker(bool closed) =>
        _ticket.Setup(t => t.GetTicketAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Ticket(new TicketId("7"), "t", "d", null, "Blocked", "Jira", isClosed: closed));

    private Task<ReworkNudgeDisposition> Handle()
    {
        var speech = new ReworkSpeech(_store.ScopeFactory, _tickets.Object, Mock.Of<ISourceProviderFactory>(), NullLogger<ReworkSpeech>.Instance);
        var fallback = new ReworkFallbackSpawn(Mock.Of<IConfigurationLoader>(), new ServerContext("c.yml"), _tickets.Object,
            new PolledTicketEnvelope(ApprovedRecordProbes.None()), Mock.Of<IEnvelopeProjectResolver>(),
            Mock.Of<ISpawnPipelineRunsUseCase>(), NullLogger<ReworkFallbackSpawn>.Instance);
        var withheld = new ReworkWithheld(_store.ScopeFactory, speech);
        var followUps = ClosedTicketFollowUps.Of(_store.ScopeFactory, _tickets.Object, speech);
        var tail = new ReworkNoActTail(_pending.Object, followUps, withheld, Mock.Of<ISeriesAutoStart>());
        var release = new PreconditionRelease(_attempts.Object, _pending.Object, tail, ReworkHandlers.Hold(_facts, _tickets.Object),
            _launch.Object, _log);
        return ReworkHandlers.Of(Mock.Of<IActiveRunLease>(), _attempts.Object, _pending.Object, _rework.Object, speech, fallback,
            withheld, Mock.Of<IRunCheckpointStore>(), followUps, release: release).HandleAsync(Project, Nudge, CancellationToken.None);
    }

    private void Released(Times times) =>
        _launch.Verify(l => l.StartPreconditionReleaseAsync("p", "7", It.IsAny<CancellationToken>()), times);

    [Fact]
    public async Task PreconditionsMet_PendingAct_ServedAsRework()
    {
        _pending.Setup(p => p.NewestAsync(Project, "7", It.IsAny<PreviousAttempt>(), It.IsAny<CancellationToken>())).ReturnsAsync(Act);
        _rework.Setup(r => r.EnterAsync(Project, "7", Act.Act, "code", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReworkOutcome.Started("run-2"));

        (await Handle()).Should().Be(ReworkNudgeDisposition.Finish);

        _rework.Verify(r => r.EnterAsync(Project, "7", Act.Act, "code", It.IsAny<CancellationToken>()), Times.Once);
        Released(Times.Never());
    }

    [Fact]
    public async Task PreconditionsMet_NoAct_ReleasesOnce()
    {
        (await Handle()).Should().Be(ReworkNudgeDisposition.Finish);

        Released(Times.Once());
        _rework.VerifyNoOtherCalls();
        _ticket.Verify(t => t.UpdateStatusAsync(It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never,
            "the release writes nothing to the tracker beyond what every claim writes");
    }

    [Fact]
    public async Task PreconditionsMet_NoLongerStuck_Skipped()
    {
        Stuck(false);

        (await Handle()).Should().Be(ReworkNudgeDisposition.Finish);

        Released(Times.Never());
    }

    [Fact]
    public async Task PreconditionsMet_TrackerClosed_Skipped()
    {
        Tracker(closed: true);

        await Handle();

        Released(Times.Never());
    }

    [Fact]
    public async Task PreconditionsMet_ClosedByAPerson_Skipped()
    {
        await _facts.SetAsync(new TicketClosedMark("jira", "jira-7", "7", "p", Start, "Grace", null, [], false), CancellationToken.None);

        await Handle();

        Released(Times.Never());
    }

    [Fact]
    public async Task PreconditionsMet_RefusedRelease_Logged()
    {
        _launch.Setup(l => l.StartPreconditionReleaseAsync("p", "7", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReworkOutcome.Refused("no capacity"));

        (await Handle()).Should().Be(ReworkNudgeDisposition.Finish);

        _log.Warnings.Should().ContainSingle().Which.Should().Contain("image-untrusted").And.Contain("no capacity");
    }

    [Fact]
    public async Task OtherOrigin_StuckTicket_NotReleased()
    {
        var release = new PreconditionRelease(_attempts.Object, _pending.Object, null!, ReworkHandlers.Hold(_facts), _launch.Object, _log);

        (await release.ReleaseAsync(Project, Nudge with { Origin = ReworkNudgeOrigin.Sweep },
            new PreviousAttempt("run-1", RunStatuses.Failed, Start, true), CancellationToken.None)).Should().BeNull();

        Released(Times.Never());
    }
}
