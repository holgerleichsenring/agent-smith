using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

public sealed class PendingRatificationScannerTests
{
    [Theory]
    [InlineData("chose: X (default pending operator ratification)", 1)]
    [InlineData("Pending Ratification by the owner", 1)]
    [InlineData("ratification pending; pending ratification", 2)]
    public void Count_EachMarkerForm_Counts(string text, int expected) =>
        PendingRatificationScanner.Count(text).Should().Be(expected);

    [Fact]
    public void Count_AColumnDefaultOrOrdinaryText_CountsZero() =>
        PendingRatificationScanner.Count("verdicts: pending | ratified | overruled, default pending").Should().Be(0);
}
