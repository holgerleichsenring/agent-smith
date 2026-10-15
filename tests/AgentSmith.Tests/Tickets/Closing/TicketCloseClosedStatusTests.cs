using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets.Closing;

/// <summary>
/// 2026-10-15-114fe: "also close on the tracker" moves a Jira or Azure DevOps ticket into the tracker's closed_status
/// when one is set — the same marked resolution, then one transition — and into the done status otherwise.
/// </summary>
public sealed class TicketCloseClosedStatusTests : IDisposable
{
    private const string Resolution = "Agent Smith — closed by Ada: done elsewhere";
    private readonly TicketCloseFixture _f = new();

    public void Dispose() => _f.Dispose();

    private async Task<TicketCloseAnswer> CloseOnTrackerAsync(TrackerType type, string? closedStatus)
    {
        _f.H.Config.Projects["alpha"] = _f.H.Config.Projects["alpha"] with
        {
            Tracker = new TrackerConnection { Name = TicketCloseFixture.Tracker, Type = type, ClosedStatus = closedStatus },
        };
        await _f.H.AddAsync(Run("r-1", "42", "failed"), Step("r-1"));
        var result = await _f.CloseAsync(note: "done elsewhere", tracker: true);
        result.Status.Should().Be(StatusCodes.Status200OK, result.Refusal);
        return (TicketCloseAnswer)result.Body!;
    }

    [Theory]
    [InlineData(TrackerType.Jira)]
    [InlineData(TrackerType.AzureDevOps)]
    public async Task TicketClose_CloseOnTrackerWithAClosedStatus_TransitionsIntoIt(TrackerType type)
    {
        _f.H.Tracker.Setup(t => t.TransitionToAsync(It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var outcome = (await CloseOnTrackerAsync(type, "Won't Do")).Tracker;

        outcome.Should().Be(new TicketTrackerCloseOutcome(true, null));
        _f.H.Tracker.Verify(t => t.UpdateStatusAsync(new TicketId("42"), Resolution, CancellationToken.None), Times.Once);
        _f.H.Tracker.Verify(t => t.TransitionToAsync(new TicketId("42"), "Won't Do", CancellationToken.None), Times.Once);
        _f.H.Tracker.Verify(t => t.CloseTicketAsync(It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TicketClose_CloseOnTrackerWithAClosedStatusNoTransition_IsRefused()
    {
        _f.H.Tracker.Setup(t => t.TransitionToAsync(It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var answer = await CloseOnTrackerAsync(TrackerType.Jira, "Won't Do");

        answer.Tracker.Should().Be(new TicketTrackerCloseOutcome(false, TicketCloseReasons.Refused));
        answer.Mark.TrackerClosed.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task TicketClose_CloseOnTrackerWithoutAClosedStatus_ClosesIntoTheDoneStatus(string? closedStatus)
    {
        var outcome = (await CloseOnTrackerAsync(TrackerType.Jira, closedStatus)).Tracker;

        outcome.Should().Be(new TicketTrackerCloseOutcome(true, null));
        _f.H.Tracker.Verify(t => t.CloseTicketAsync(new TicketId("42"), Resolution, CancellationToken.None), Times.Once);
        _f.H.Tracker.Verify(t => t.TransitionToAsync(It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TicketClose_CloseOnAGitHubTracker_IgnoresTheClosedStatus()
    {
        var outcome = (await CloseOnTrackerAsync(TrackerType.GitHub, "Won't Do")).Tracker;

        outcome.Should().Be(new TicketTrackerCloseOutcome(true, null));
        _f.H.Tracker.Verify(t => t.CloseTicketAsync(new TicketId("42"), Resolution, CancellationToken.None), Times.Once);
        _f.H.Tracker.Verify(t => t.TransitionToAsync(It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
