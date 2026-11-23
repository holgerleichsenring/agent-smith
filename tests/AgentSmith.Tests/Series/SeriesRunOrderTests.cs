using AgentSmith.Application.Services.Series;
using FluentAssertions;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-23-31dcb: a series' depth is its longest requires path inside the series; one open pull request at a
/// time is ready to merge, the rest say what they wait for.</summary>
public sealed class SeriesRunOrderTests
{
    private readonly SeriesRunOrder _order = new();

    [Fact]
    public void Depths_Chain_CountsTheLongestPath()
    {
        var depths = _order.Depths([T("1"), T("2", "1"), T("3", "2")]);

        depths.Should().Equal(new Dictionary<string, int> { ["1"] = 0, ["2"] = 1, ["3"] = 2 });
    }

    [Fact]
    public void Depths_Diamond_TakesTheLongerBranch()
    {
        var depths = _order.Depths([T("1"), T("2", "1"), T("3", "2"), T("4", "1", "3")]);

        depths["4"].Should().Be(3, "1 -> 2 -> 3 -> 4 is longer than 1 -> 4");
    }

    [Fact]
    public void Depths_NoRequiresOrOnlyOutsideTheSeries_IsZero()
    {
        var depths = _order.Depths([T("1"), T("2", "elsewhere")]);

        depths["1"].Should().Be(0);
        depths["2"].Should().Be(0, "an edge leaving the series adds no depth");
    }

    [Fact]
    public void Depths_ACycle_EndsInsteadOfLooping()
    {
        var depths = _order.Depths([T("1", "2"), T("2", "1")]);

        depths.Should().HaveCount(2);
    }

    [Fact]
    public void MergeOrder_OnlyTheLowestMergeableIsReady_TheOthersSayAfterWhat()
    {
        var steps = _order.MergeOrder(
            [Open("3", "2"), Open("1"), Open("2", "1"), Open("4"), T("5", "1")], new HashSet<string>());

        steps.Select(s => s.TicketId).Should().Equal(["1", "4", "2", "3"], "depth first, then series order; no open PR, no step");
        (steps[0].Wait, steps[0].Depth, steps[0].After.Count).Should().Be((SeriesRunMergeWaits.Ready, 0, 0));
        steps[1].Wait.Should().Be(SeriesRunMergeWaits.After);
        steps[1].After.Should().Equal(["K-1"], "it could merge, but one at a time: after the ready one");
        steps[2].After.Should().Equal(["K-1"], "its requirement has not landed");
        steps[3].After.Should().Equal(["K-2"]);
    }

    [Fact]
    public void MergeOrder_RequirementsLanded_ButTargetingAnotherBranch_WaitsForTheRetarget()
    {
        var steps = _order.MergeOrder([Open("2", "1") with { TargetsElsewhere = true }, Open("3", "1")], new HashSet<string> { "1" });

        steps[0].Wait.Should().Be(SeriesRunMergeWaits.Retarget);
        steps[1].Wait.Should().Be(SeriesRunMergeWaits.Ready, "the one targeting another branch is not ready, so the next one is");
    }

    [Fact]
    public void MergeOrder_LandedTicket_IsNotAStep_AndARequirementOutsideTheSeriesIsNamedById()
    {
        var steps = _order.MergeOrder([Open("1"), Open("2", "outside")], new HashSet<string> { "1" });

        steps.Should().ContainSingle().Which.After.Should().Equal(["outside"]);
    }

    private static SeriesRunOrderTicket T(string id, params string[] requires) => new(id, $"K-{id}", requires);

    private static SeriesRunOrderTicket Open(string id, params string[] requires) => T(id, requires) with { HasOpenPullRequest = true };
}
