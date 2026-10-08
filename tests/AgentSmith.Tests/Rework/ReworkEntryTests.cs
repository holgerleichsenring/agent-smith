using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Rework;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Rework;

/// <summary>2026-10-08-e8b9b: the rework entry's decisions, each named by the case it answers.</summary>
public sealed class ReworkEntryTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 8, 9, 0, 0, TimeSpan.Zero);
    private readonly Mock<IActiveRunLease> _leases = new();
    private readonly Mock<IPreviousAttemptReader> _attempts = new();
    private readonly Mock<IReworkParkCheck> _parks = new();
    private readonly Mock<ITicketReopener> _reopener = new();
    private readonly Mock<IReworkLaunch> _launch = new();
    private static readonly ResolvedProject Project = new() { Name = "p", Tracker = new TrackerConnection { Type = TrackerType.Jira } };

    public ReworkEntryTests()
    {
        _launch.Setup(l => l.LaunchAsync("p", "T-1", "code", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReworkOutcome.Started("run-2"));
        _parks.Setup(p => p.MoveAsync(Project, "T-1", It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    private ReworkEntry Entry() => new(_leases.Object, _attempts.Object, _parks.Object, _reopener.Object,
        _launch.Object, NullLogger<ReworkEntry>.Instance);

    private void Attempt(string status, bool finished, string runId = "run-1") =>
        _attempts.Setup(a => a.LatestAsync("p", "T-1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PreviousAttempt(runId, status, Start, finished));

    private void Live(string runId) => _leases.Setup(l => l.GetByTicketAsync("p", It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new StaleLease("p", new TicketId("T-1"), runId, null, Start));

    private void Park(bool parked, string? moveTo) =>
        _parks.Setup(p => p.CheckAsync(Project, "T-1", It.IsAny<CancellationToken>())).ReturnsAsync(new ReworkPark(parked, moveTo));

    private Task<ReworkOutcome> Act(int minutesAfterStart) =>
        Entry().EnterAsync(Project, "T-1", new ReworkAct("alice", Start.AddMinutes(minutesAfterStart)), "code", CancellationToken.None);

    [Fact]
    public async Task ReworkEntry_LiveLeaseActNewer_RefusesNamingRun()
    {
        Attempt("running", false, "run-live"); Live("run-live");

        var outcome = await Act(10);

        outcome.Kind.Should().Be(ReworkOutcomeKind.Refused);
        outcome.RunId.Should().Be("run-live");
        outcome.Reason.Should().Contain("run-live");
    }

    [Fact]
    public async Task ReworkEntry_LiveLeaseActOlder_AlreadyServed()
    {
        Attempt("running", false, "run-live"); Live("run-live");

        (await Act(-5)).Kind.Should().Be(ReworkOutcomeKind.AlreadyServed);
    }

    [Fact]
    public async Task ReworkEntry_ParkedAttempt_RefusesVisibly()
    {
        Attempt(RunStatuses.WaitingForInput, false);

        var outcome = await Act(10);

        outcome.Kind.Should().Be(ReworkOutcomeKind.Refused);
        outcome.Reason.Should().Contain("waiting for an answer");
        _launch.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReworkEntry_ActWithinSkewOfStart_AlreadyServed()
    {
        Attempt("success", true);

        (await Act(1)).Kind.Should().Be(ReworkOutcomeKind.AlreadyServed);
    }

    [Fact]
    public async Task ReworkEntry_VerdictPark_NotARework()
    {
        Attempt("success", true); Park(parked: false, moveTo: "To Do");

        (await Act(10)).Kind.Should().Be(ReworkOutcomeKind.NotARework);
        _reopener.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReworkEntry_NoAttempt_NotARework()
    {
        (await Act(10)).Kind.Should().Be(ReworkOutcomeKind.NotARework);
    }

    [Fact]
    public async Task ReworkEntry_DoneJiraTicket_MovesClearsAndClaims()
    {
        Attempt("success", true); Park(parked: true, moveTo: "To Do");

        var outcome = await Act(10);

        outcome.Should().Be(ReworkOutcome.Started("run-2"));
        _parks.Verify(p => p.MoveAsync(Project, "T-1", "To Do", It.IsAny<CancellationToken>()), Times.Once);
        _reopener.Verify(r => r.ClearAsync(Project, "T-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReworkEntry_CurrentStatusTrigger_DoesNotMove()
    {
        Attempt("failed", true); Park(parked: true, moveTo: null);

        (await Act(10)).Kind.Should().Be(ReworkOutcomeKind.Started);
        _parks.Verify(p => p.MoveAsync(It.IsAny<ResolvedProject>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReworkEntry_RefusedMove_ClearsNothing()
    {
        Attempt("success", true); Park(parked: true, moveTo: "To Do");
        _parks.Setup(p => p.MoveAsync(Project, "T-1", "To Do", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var outcome = await Act(10);

        outcome.Kind.Should().Be(ReworkOutcomeKind.Refused);
        _reopener.VerifyNoOtherCalls();
        _launch.VerifyNoOtherCalls();
    }
}
