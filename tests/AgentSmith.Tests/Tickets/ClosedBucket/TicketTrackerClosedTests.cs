using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets.Checks;
using AgentSmith.Server.Services.Tickets.Closing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using static AgentSmith.Tests.Tickets.ClosedBucket.ClosedFixtures;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets.ClosedBucket;

/// <summary>
/// 2026-10-15-114fc: the between-attempts check records whether a person closed the ticket for work
/// (TicketClosure on the ticket it already read), and the list shows a failed ticket so closed as Closed (tracker);
/// a close into agent-smith's own closed status, or one by the token with no done status configured, never is.
/// </summary>
public sealed class TicketTrackerClosedTests : IDisposable
{
    private static readonly TrackerActor Token = new("agent-id", "agent");
    private readonly TicketStoreHarness _h = new();

    public TicketTrackerClosedTests()
    {
        _h.Tracker.As<ITicketStatusHistory>();
        _h.Tracker.As<ITrackerSelf>().Setup(s => s.SelfAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Token);
        _h.Tracker.Setup(t => t.GetCommentsAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<TicketComment>());
    }

    public void Dispose() => _h.Dispose();

    private static Ticket Text(string status, bool closed) => new(new TicketId("42"), "Ticket 42", "Export it.", null, status, "github", isClosed: closed);

    private void Project(TrackerType type = TrackerType.GitHub, string? doneStatus = "Done", string? closedStatus = null) =>
        _h.Config.Projects["alpha"] = new ResolvedProject
        {
            Name = "alpha", Tracker = new TrackerConnection { Name = "gh", Type = type, ClosedStatus = closedStatus },
            GithubTrigger = doneStatus is null ? null : new WebhookTriggerConfig { DoneStatus = doneStatus },
            JiraTrigger = doneStatus is null ? null : new JiraTriggerConfig { DoneStatus = doneStatus },
        };

    private Task FailedAsync(Ticket ticket, string platform = "github")
    {
        var run = Run("r-1", status: "failed", platform: platform);
        run.ActsReadAt = T;
        return _h.AddAsync(run, Step("r-1"), new RunAttempt { RunId = "r-1", TicketFingerprint = TicketTextFingerprint.Of(ticket) });
    }

    private async Task<bool> CheckAsync(Ticket ticket, bool add = true)
    {
        if (add) await FailedAsync(ticket);
        _h.Tracker.Setup(t => t.GetTicketAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        await _h.Checker.CheckAsync("alpha", "42", default);
        return (await _h.ReadAllAsync<TicketBetweenAttemptsCheck>()).Single().ClosedForWork;
    }

    [Fact]
    public async Task TicketTrackerObserver_PersonClosedTheTicket_RecordsClosedForWork()
    {
        Project();

        (await CheckAsync(Text("Won't Fix", closed: true))).Should().BeTrue();
    }

    [Fact]
    public async Task TicketTrackerObserver_AgentSmithClosedItWithNoDoneStatus_RecordsNotClosedForWork()
    {
        Project(doneStatus: null);
        _h.Tracker.As<ITicketStatusHistory>().Setup(h => h.NewestMoveIntoAsync(new TicketId("42"), "closed", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketStatusMove(Token, T.AddHours(1), "closed"));

        (await CheckAsync(Text("closed", closed: true))).Should().BeFalse("the token moved it there");
    }

    [Fact]
    public async Task TicketTrackerObserver_TicketInTheTrackersClosedStatus_RecordsNotClosedForWork()
    {
        Project(closedStatus: "Won't Do");

        (await CheckAsync(Text("Won't Do", closed: true))).Should().BeFalse("the closed status is agent-smith's own");
    }

    [Fact]
    public async Task TicketCheckStore_ClosedForWorkChanged_AnswersChanged()
    {
        var store = _h.Services.GetRequiredService<TicketCheckStore>();
        await store.WriteAsync("r-1", T, new TicketObservation(true, []), default);

        (await store.WriteAsync("r-1", T.AddMinutes(5), new TicketObservation(true, []), default)).Should().BeFalse();
        (await store.WriteAsync("r-1", T.AddMinutes(10), new TicketObservation(true, [], ClosedForWork: true), default)).Should().BeTrue();
        (await store.ForAsync(["r-1"], default))["r-1"].ClosedForWork.Should().BeTrue();
    }

    [Fact]
    public async Task TicketList_FailedTicketAPersonClosedOnTheTracker_IsClosedAfterItsCheck()
    {
        Project();
        await CheckAsync(Text("Won't Fix", closed: true));

        var row = await _h.RowAsync();

        (row.Bucket, row.Kind, row.Closed).Should().Be((TicketBucket.Closed, TicketClosedKinds.Tracker, (TicketClosedView?)null));
    }

    [Fact]
    public async Task TicketList_ClosedByTheActIntoTheClosedStatusThenReopened_IsFailed()
    {
        Project(TrackerType.Jira, closedStatus: "Won't Do");
        _h.Tracker.Setup(t => t.TransitionToAsync(It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var act = _h.Services.GetRequiredService<TicketCloseAct>();
        var closedOnTheTracker = Text("Won't Do", closed: true);
        // The ticket was edited since the attempt, so the check reads it changed, never stuck.
        await FailedAsync(new Ticket(new TicketId("42"), "Ticket 42", "Export it twice.", null, "Open", "jira"), platform: "jira");

        (await act.CloseAsync("alpha", "42", new TicketCloseRequest(null, false, true), "Ada", default)).Status.Should().Be(200);
        (await act.ReopenAsync("alpha", "42", default)).Status.Should().Be(200);
        _h.Tracker.Verify(t => t.TransitionToAsync(new TicketId("42"), "Won't Do", CancellationToken.None), Times.Once);
        (await CheckAsync(closedOnTheTracker, add: false)).Should().BeFalse();

        (await _h.RowAsync()).Bucket.Should().Be(TicketBucket.Failed);
    }
}
