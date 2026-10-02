using System.Text.Json;
using AgentSmith.Application.Services.Design;
using FluentAssertions;

namespace AgentSmith.Tests.DesignSources;

/// <summary>2026-10-01-7f7ac: the export scale keeps the render's long edge within what the image path takes.</summary>
public sealed class FigmaExportScaleTests
{
    [Theory]
    [InlineData(3136, 100, 0.5)]
    [InlineData(100, 3136, 0.5)]
    [InlineData(1919, 1080, 0.817)]
    [InlineData(5000, 1000, 0.3136)]
    [InlineData(360, 56, 2)]
    [InlineData(1_000_000, 10, 0.01)]
    public void FigmaExport_ScaleBoundsLongestSideTo1568(double width, double height, double expected)
    {
        var scale = FigmaExportScale.For(Nodes($$"""{"width":{{width}},"height":{{height}}}"""), "1:2");

        scale.Should().BeApproximately(expected, 1e-9);
        if (expected > FigmaExportScale.MinScale)
            (Math.Max(width, height) * scale).Should().BeLessThanOrEqualTo(FigmaExportScale.MaxLongEdge);
    }

    [Fact]
    public void FigmaExport_NodeWithoutBounds_IsExportedAtOne() =>
        FigmaExportScale.For(Nodes("null"), "1:2").Should().Be(1);

    private static JsonElement Nodes(string box) => JsonDocument.Parse(
        "{\"nodes\":{\"1:2\":{\"document\":{\"id\":\"1:2\",\"absoluteBoundingBox\":" + box + "}}}}").RootElement;
}
