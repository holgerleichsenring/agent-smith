using AgentSmith.Application.Services.Polling;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Polling;

public sealed class LifecyclePollFilterTests
{
    [Fact]
    public void IsClaimableLifecycle_NoLabels_ReturnsTrue()
        => LifecyclePollFilter.IsClaimableLifecycle([]).Should().BeTrue();

    [Fact]
    public void IsClaimableLifecycle_OnlyUserLabels_ReturnsTrue()
        => LifecyclePollFilter.IsClaimableLifecycle(["fix", "feature"]).Should().BeTrue();

    [Fact]
    public void IsClaimableLifecycle_PendingLifecycle_ReturnsTrue()
        => LifecyclePollFilter.IsClaimableLifecycle(["agent-smith:pending"]).Should().BeTrue();

    [Theory]
    [InlineData("agent-smith:enqueued")]
    [InlineData("agent-smith:in-progress")]
    [InlineData("agent-smith:done")]
    [InlineData("agent-smith:failed")]
    public void IsClaimableLifecycle_NonPendingLifecycle_ReturnsFalse(string lifecycleLabel)
        => LifecyclePollFilter.IsClaimableLifecycle([lifecycleLabel]).Should().BeFalse();

    [Fact]
    public void IsClaimableLifecycle_UnknownAgentSmithSuffix_TreatedAsNeutral()
    {
        // Defensive: an unrecognized agent-smith:* label should not block claim.
        // Behavior keeps poller resilient to schema additions in newer versions.
        LifecyclePollFilter.IsClaimableLifecycle(["agent-smith:future-state"]).Should().BeTrue();
    }

    [Fact]
    public void KeepClaimable_FiltersOutInProgressTickets()
    {
        var tickets = new[]
        {
            new Ticket(new TicketId("1"), "a", "", null, "Open", "GitHub", labels: ["fix"]),
            new Ticket(new TicketId("2"), "b", "", null, "Open", "GitHub", labels: ["fix", "agent-smith:in-progress"]),
            new Ticket(new TicketId("3"), "c", "", null, "Open", "GitHub", labels: ["fix", "agent-smith:pending"])
        };

        var kept = LifecyclePollFilter.KeepClaimable(tickets).Select(t => t.Id.Value).ToArray();

        kept.Should().BeEquivalentTo(["1", "3"]);
    }
}
