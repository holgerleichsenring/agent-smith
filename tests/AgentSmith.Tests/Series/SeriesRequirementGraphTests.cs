using AgentSmith.Server.Services.Series;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-25-9992f: a ticket reaches what it waits on, directly or through others, and itself; an added or removed
/// edge changes that.</summary>
public sealed class SeriesRequirementGraphTests
{
    [Fact]
    public void Reaches_FollowsEdgesTransitively()
    {
        var graph = new SeriesRequirementGraph([TicketRequirementEdges.Edge("c", "b"), TicketRequirementEdges.Edge("b", "a")]);

        graph.Reaches("C", "A").Should().BeTrue();
        graph.Reaches("A", "C").Should().BeFalse();
        graph.Reaches("A", "A").Should().BeTrue("a ticket reaches itself");
    }

    [Fact]
    public void AddAndRemove_ChangeWhatIsReached()
    {
        var graph = new SeriesRequirementGraph([TicketRequirementEdges.Edge("b", "a")]);

        graph.Add("A", "C");
        graph.Reaches("B", "C").Should().BeTrue();
        graph.Remove("B", "A");
        graph.Reaches("B", "C").Should().BeFalse();
    }
}
