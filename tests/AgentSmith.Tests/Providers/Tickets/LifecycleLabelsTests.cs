using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;

namespace AgentSmith.Tests.Providers.Tickets;

public sealed class LifecycleLabelsTests
{
    [Theory]
    [InlineData(TicketLifecycleStatus.Pending, "agent-smith:pending")]
    [InlineData(TicketLifecycleStatus.Enqueued, "agent-smith:enqueued")]
    [InlineData(TicketLifecycleStatus.InProgress, "agent-smith:in-progress")]
    [InlineData(TicketLifecycleStatus.Done, "agent-smith:done")]
    [InlineData(TicketLifecycleStatus.Failed, "agent-smith:failed")]
    public void For_AllStatuses_ReturnsExpectedLabel(TicketLifecycleStatus status, string expected)
    {
        LifecycleLabels.For(status).Should().Be(expected);
    }

    [Theory]
    [InlineData("agent-smith:pending", TicketLifecycleStatus.Pending, true)]
    [InlineData("agent-smith:in-progress", TicketLifecycleStatus.InProgress, true)]
    [InlineData("agent-smith:done", TicketLifecycleStatus.Done, true)]
    [InlineData("agent-smith:bogus", default(TicketLifecycleStatus), false)]
    [InlineData("bug", default(TicketLifecycleStatus), false)]
    public void TryParse_ValidAndInvalid_BehavesAsExpected(
        string label, TicketLifecycleStatus expected, bool ok)
    {
        var success = LifecycleLabels.TryParse(label, out var status);

        success.Should().Be(ok);
        if (ok) status.Should().Be(expected);
    }

    [Fact]
    public void IsLifecycleLabel_PrefixOnly_ReturnsTrue()
    {
        LifecycleLabels.IsLifecycleLabel("agent-smith:anything").Should().BeTrue();
        LifecycleLabels.IsLifecycleLabel("bug").Should().BeFalse();
    }
}
