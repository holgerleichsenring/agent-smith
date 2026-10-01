using System.Text.Json;
using AgentSmith.Application.Services.Design;
using FluentAssertions;

namespace AgentSmith.Tests.DesignSources;

/// <summary>2026-10-01-7f7ab: the pure summaries over the documented response shapes.</summary>
public sealed class FigmaSummaryTests
{
    [Fact]
    public void FigmaVariableSummary_Names_MapsIdToName()
    {
        var names = FigmaVariableSummary.Names(Parse(FigmaFakes.Variables));

        names.Should().Contain("VariableID:1:5", "space/sm").And.Contain("VariableID:1:6", "color/brand");
    }

    [Fact]
    public void FigmaVariableSummary_Render_ValuesPerMode()
    {
        var text = FigmaVariableSummary.Render(Parse(FigmaFakes.Variables), 6_000);

        text.Should().StartWith("variables: 2").And.Contain("  space/sm: Light 8 · Dark 8");
    }

    [Fact]
    public void FigmaVariableSummary_OverBudget_StatesTruncation() =>
        FigmaVariableSummary.Render(Parse(FigmaFakes.Variables), 60).Should()
            .Contain("[truncated: variables beyond the 60-character budget are not shown]");

    [Fact]
    public void FigmaNodeSummary_UnknownVariable_ShowsItsId()
    {
        var text = FigmaNodeSummary.Render(Parse(FigmaFakes.Nodes), new Dictionary<string, string>(), 16_000);

        text.Should().Contain("version: 4242").And.Contain("variables itemSpacing=VariableID:1:5");
    }

    [Fact]
    public void FigmaNodeSummary_NodeMissingFromFile_SaysSo() =>
        FigmaNodeSummary.Render(Parse("{\"name\":\"F\",\"version\":\"1\",\"nodes\":{\"9:9\":null}}"),
                new Dictionary<string, string>(), 1_000)
            .Should().Contain("node 9:9: not in this file version");

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();
}
