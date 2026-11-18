using AgentSmith.Server.Services.Series;
using FluentAssertions;

namespace AgentSmith.Tests.Series;

public sealed class SeriesTicketFactsLandedTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 18, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void LandedAt_TicketWithoutLanding_IsNull() =>
        SeriesTicketFacts.LandedAt(new Dictionary<string, DateTimeOffset> { ["1"] = At }, "2").Should().BeNull();

    [Fact]
    public void LandedAt_LandedTicket_IsItsLandingTime() =>
        SeriesTicketFacts.LandedAt(new Dictionary<string, DateTimeOffset> { ["1"] = At }, "1").Should().Be(At);
}
