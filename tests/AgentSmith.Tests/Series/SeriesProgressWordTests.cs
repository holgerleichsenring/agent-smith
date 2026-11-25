using AgentSmith.Application.Services.Series;
using FluentAssertions;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-25-433fa: merged over total, said as the nearest mark; none before the first merge, all at the end.</summary>
public sealed class SeriesProgressWordTests
{
    [Theory]
    [InlineData(0, 25, "none")]
    [InlineData(0, 0, "none")]
    [InlineData(1, 25, "begun")]
    [InlineData(6, 25, "quarter")]
    [InlineData(9, 25, "third")]
    [InlineData(12, 25, "half")]
    [InlineData(17, 25, "twoThirds")]
    [InlineData(19, 25, "threeQuarters")]
    [InlineData(24, 25, "nearly")]
    [InlineData(25, 25, "all")]
    public void Of_MergedOverTotal_TheNearestWord(int merged, int total, string word) =>
        SeriesProgressWord.Of(merged, total).Should().Be(word);
}
