using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>2026-10-25-9dbba: the ticket reference a caller hands the code host.</summary>
public sealed class PullRequestTicketTests
{
    [Fact]
    public void From_FetchedTicket_CarriesIdTrackerAndWebUrl()
    {
        var ticket = new Ticket(new TicketId("4711"), "t", "d", null, "New", "AzureDevOps")
            .With(webUrl: "https://dev.azure.com/example/demo/_workitems/edit/4711");

        PullRequestTicket.From(ticket).Should().Be(new PullRequestTicket(
            new TicketId("4711"), TrackerType.AzureDevOps, "https://dev.azure.com/example/demo/_workitems/edit/4711"));
    }

    [Fact]
    public void From_UnknownSource_HasNoTracker() =>
        PullRequestTicket.From(new Ticket(new TicketId("1"), "t", "d", null, "New", "Trello"))!
            .Tracker.Should().BeNull();

    [Fact]
    public void From_NoTicket_IsNull() => PullRequestTicket.From(null).Should().BeNull();

    [Fact]
    public void Of_IdAndTracker_HasNoWebUrl() =>
        PullRequestTicket.Of(new TicketId("7"), TrackerType.GitLab)
            .Should().Be(new PullRequestTicket(new TicketId("7"), TrackerType.GitLab, null));

    [Fact]
    public void Of_NoId_IsNull() => PullRequestTicket.Of(null, TrackerType.GitHub).Should().BeNull();
}
