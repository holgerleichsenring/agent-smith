using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

/// <summary>2026-11-25-9992i: review's YAML spelling — end by default, an unknown value read as none of them.</summary>
public sealed class ReviewModeNamesTests
{
    [Theory]
    [InlineData(null, ReviewMode.End)]
    [InlineData("", ReviewMode.End)]
    [InlineData(" End ", ReviewMode.End)]
    [InlineData("each", ReviewMode.Each)]
    [InlineData("NONE", ReviewMode.None)]
    public void Parse_ReadsEveryName_AndTheDefault(string? written, ReviewMode expected) =>
        ReviewModeNames.Parse(written).Should().Be(expected);

    [Fact]
    public void Parse_AnUnknownValue_IsNull() => ReviewModeNames.Parse("on_ci_green").Should().BeNull();

    [Fact]
    public void Of_And_All_NameEveryModeInEnumOrder() =>
        Enum.GetValues<ReviewMode>().Select(ReviewModeNames.Of).Should().Equal(ReviewModeNames.All).And.Equal("end", "each", "none");

    [Fact]
    public void Written_IsTheValueOrTheDefault()
    {
        ReviewModeNames.Written(null).Should().Be("end");
        ReviewModeNames.Written(" each ").Should().Be("each");
    }
}
