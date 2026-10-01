using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Services.ToolImages;
using FluentAssertions;

namespace AgentSmith.Tests.ToolImages;

/// <summary>2026-10-01-283dd: the bounds a producer must meet before an image is taken.</summary>
public sealed class ToolImageRuleTests
{
    private readonly ToolImageRule _rule = new(new ImageDimensionReader());

    [Fact]
    public void BoundedPng_IsTaken() =>
        _rule.Refusal(Image("image/png", ToolImageLoopFixture.Png(1568, 900))).Should().BeNull();

    [Theory]
    [InlineData("image/svg+xml", "media type")]
    [InlineData("image/jpeg", "not a readable")]
    public void WrongTypeOrMislabelled_IsRefused(string mediaType, string reason) =>
        _rule.Refusal(Image(mediaType, ToolImageLoopFixture.Png(10, 10))).Should().Contain(reason);

    [Fact]
    public void LongEdgeOver1568_IsRefusedNamingTheSize() =>
        _rule.Refusal(Image("image/png", ToolImageLoopFixture.Png(900, 2700)))
            .Should().Contain("900x2700").And.Contain("1568");

    [Fact]
    public void OverFiveMegabytes_IsRefused()
    {
        var bytes = new byte[TicketImageAttachment.MaxSizeBytes + 1];
        ToolImageLoopFixture.Png(10, 10).CopyTo(bytes, 0);
        _rule.Refusal(Image("image/png", bytes)).Should().Contain("byte limit");
    }

    [Fact]
    public void Empty_IsRefused() => _rule.Refusal(Image("image/png", [])).Should().Contain("empty");

    private static ToolImage Image(string mediaType, byte[] bytes) => new(mediaType, bytes, "caption");
}
