using AgentSmith.Application.Services.Tickets;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Tickets.Closure;

/// <summary>2026-10-13-9a59k: the one resolver of the statuses agent-smith sets — trigger, then tracker.</summary>
public sealed class TicketParkStatusesTests
{
    private static ResolvedProject Project(
        JiraTriggerConfig? trigger, string? clarification = null, string? notImplementable = null, string? closed = null) => new()
    {
        Name = "p",
        Tracker = new TrackerConnection
        {
            Name = "jira", Type = TrackerType.Jira,
            NeedsClarificationStatus = clarification, NotImplementableStatus = notImplementable, ClosedStatus = closed,
        },
        JiraTrigger = trigger,
    };

    [Fact]
    public void TicketParkStatuses_TriggerOverridesTrackerStatus()
    {
        var parks = TicketParkStatuses.Of(Project(
            new JiraTriggerConfig { DoneStatus = "Done", NeedsClarificationStatus = "Ask", NotImplementableStatus = "Won't do" },
            clarification: "Tracker ask", notImplementable: "Tracker won't do"));

        parks.Should().Be(new TicketParkStatuses("Done", "Done", "Ask", "Won't do"), "failed_status unset falls back to done, as the spawn seeds it");
        parks.Own("tracker ask").Should().BeFalse();
        parks.Own("won't DO").Should().BeTrue();
    }

    [Fact]
    public void TicketParkStatuses_NotImplementableUnset_FallsBackToClarification()
    {
        var parks = TicketParkStatuses.Of(Project(
            new JiraTriggerConfig { DoneStatus = "", FailedStatus = "Failed" }, clarification: "Tracker ask"));

        parks.Should().Be(new TicketParkStatuses(null, "Failed", "Tracker ask", "Tracker ask"));
        parks.IsHandbackPark("Tracker ask").Should().BeTrue();
        TicketParkStatuses.Of(Project(null)).Should().Be(new TicketParkStatuses(null, null, null, null),
            "no trigger, nothing configured: agent-smith sets no status");
    }

    [Fact]
    public void TicketParkStatuses_ClosedStatus_IsOwnButNotARunEnd()
    {
        var parks = TicketParkStatuses.Of(Project(new JiraTriggerConfig { DoneStatus = "Done" }, closed: "Won't Do"));

        parks.Closed.Should().Be("Won't Do");
        parks.Own("won't do").Should().BeTrue();
        parks.IsRunEnd("Won't Do").Should().BeFalse("a keyword comment on it is not a rework");
        parks.IsHandbackPark("Won't Do").Should().BeFalse();
        TicketParkStatuses.Of(Project(null, closed: " ")).Closed.Should().BeNull("a blank closed_status is none");
    }
}
