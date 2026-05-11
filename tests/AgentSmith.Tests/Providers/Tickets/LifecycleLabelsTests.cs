using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Tickets;
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

    [Theory]
    [InlineData("agent-smith:pending", true)]
    [InlineData("agent-smith:enqueued", true)]
    [InlineData("agent-smith:in-progress", true)]
    [InlineData("agent-smith:done", true)]
    [InlineData("agent-smith:failed", true)]
    [InlineData("agent-smith:init", false)]               // operator trigger label (p0133)
    [InlineData("agent-smith:bug", false)]                // operator trigger label
    [InlineData("agent-smith:no-test-adaption", false)]   // existing triage-override convention
    [InlineData("agent-smith:skip:tester", false)]        // existing triage-override convention
    [InlineData("agent-smith:bogus", false)]
    [InlineData("bug", false)]
    public void IsLifecycleLabel_OnlyClosedSetMatches_OperatorPrefixedLabelsPassThrough(string label, bool expected)
    {
        LifecycleLabels.IsLifecycleLabel(label).Should().Be(expected);
    }
}
