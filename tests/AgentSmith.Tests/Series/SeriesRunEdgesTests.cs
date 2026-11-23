using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Series;
using FluentAssertions;
using static AgentSmith.Tests.TestSupport.TicketRequirementEdges;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-23-31dcf: the requires edges inside a series. 2026-11-23-f562: read from the facts alone, without a
/// kind — an edge to a ticket outside the series is not one of its edges, and a prerequisite named twice is one edge.</summary>
public sealed class SeriesRunEdgesTests
{
    [Fact]
    public void Of_KeepsTheEdgesBetweenSeriesTickets_Once()
    {
        var edges = SeriesRunEdges.Of(Facts(Edge("d", "a"), Edge("d", "a"), Edge("d", "outside")), ["D", "A"]);

        edges.Should().Equal(new SeriesRunEdgeView("D", "A"));
    }

    [Fact]
    public void Of_ADependentOutsideTheSeries_HasNoEdge()
    {
        SeriesRunEdges.Of(Facts(Edge("d", "a")), ["A"]).Should().BeEmpty();
    }

    private static SeriesRunProjectFacts Facts(params TicketRequirementEdge[] edges) =>
        new(new ResolvedProject { Name = Project, Tracker = new TrackerConnection { Name = Tracker } },
            new Dictionary<string, IReadOnlyList<string>>(), new HashSet<string>(StringComparer.Ordinal),
            Array.Empty<PrReviewRecordView>().ToLookup(r => r.TicketId), new Dictionary<string, PreviousAttempt>())
        {
            Edges = new Dictionary<string, IReadOnlyList<TicketRequirementEdge>> { ["D"] = edges },
        };
}
