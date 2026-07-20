using AgentSmith.Server.Services.Lifecycle;
using FluentAssertions;

namespace AgentSmith.Tests.Server;

/// <summary>
/// p0355: the enforced-cancel summary + ticket comment derive from the TYPED cancel
/// reason on the run row, so a stale-lease reap is not mislabeled "operator".
/// </summary>
public sealed class CancelReasonNarratorTests
{
    [Fact]
    public void StaleLeaseReap_IsNotLabeledOperator()
    {
        var summary = CancelReasonNarrator.Summary("stale-lease-reaped");

        summary.Should().Contain("Reaped");
        summary.Should().NotContain("by operator");
    }

    [Fact]
    public void OperatorCancel_ReadsAsOperator()
    {
        CancelReasonNarrator.Summary("operator").Should().Contain("by operator");
    }

    [Theory]
    [InlineData("watchdog-wall-time", "wall-time")]
    [InlineData("budget", "budget")]
    [InlineData("crashed", "crashed")]
    [InlineData("sandbox-vanished", "sandbox")]
    public void TypedReason_NamesItsCause(string reason, string expectedFragment)
    {
        CancelReasonNarrator.Summary(reason).Should().Contain(expectedFragment);
    }

    [Fact]
    public void NullReason_FallsBackToOperator()
    {
        CancelReasonNarrator.Summary(null).Should().Contain("by operator");
    }

    [Fact]
    public void StaleLeaseReap_TicketComment_IsNotOperator()
    {
        CancelReasonNarrator.TicketComment("stale-lease-reaped")
            .Should().Contain("Reaped").And.NotContain("by operator");
    }
}
