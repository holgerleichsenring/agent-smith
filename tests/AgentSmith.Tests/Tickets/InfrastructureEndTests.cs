using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Services.Tickets;
using FluentAssertions;

namespace AgentSmith.Tests.Tickets;

/// <summary>2026-10-13-9a59a: the one rule for an attempt that ended on infrastructure.</summary>
public sealed class InfrastructureEndTests
{
    [Theory]
    [InlineData("cancelled", "interrupted", true)]
    [InlineData("cancelled", "stale-lease-reaped", true)]
    [InlineData("cancelled", "operator", false)]
    [InlineData("cancelled", null, false)]
    [InlineData("failed", "interrupted", false)]
    public void InfrastructureEnd_InterruptedOrStaleLease_Holds_UserCancelDoesNot(
        string status, string? reason, bool holds)
    {
        var run = new Run { Id = "r", Status = status, CancelReason = reason };

        new InfrastructureEnd().Holds(run).Should().Be(holds);
    }
}
