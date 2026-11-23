using AgentSmith.Application.Services.Holds;
using AgentSmith.Application.Services.Series;
using FluentAssertions;

namespace AgentSmith.Tests.Tickets.Holds;

/// <summary>2026-11-23-8d16: the merges a ticket that starts on merges needs first — every step of the order up to the last one
/// it names — and the hold's merge count reads the same rule.</summary>
public sealed class TicketSeriesMergeHoldNeededTests
{
    private static readonly SeriesRunMergeStep[] Order =
    [
        new("42", "#42", 0, SeriesRunMergeWaits.Ready, []), new("43", "#43", 1, SeriesRunMergeWaits.After, ["#42"]),
        new("45", "#45", 2, SeriesRunMergeWaits.After, ["#43"]), new("46", "#46", 3, SeriesRunMergeWaits.After, ["#45"]),
    ];

    [Fact]
    public void Needed_MarksEveryStepUpToTheLastOneItNames()
    {
        TicketSeriesMergeHold.Needed(Order, ["#43", "#45"]).Select(s => s.Key).Should().Equal("#42", "#43", "#45");
        TicketSeriesMergeHold.Needed(Order, ["#42"]).Select(s => s.Key).Should().Equal("#42");
        TicketSeriesMergeHold.Needed(Order, ["#60"]).Should().BeEmpty("a merge without a pull request is in no order yet");
    }

    [Fact]
    public void StartsOnMerge_CountsTheNeededStepsAndTheNeedsWithoutAStep()
    {
        var hold = TicketSeriesMergeHold.StartsOnMerge(new TicketHoldFacts("alpha", "50")
        {
            SeriesId = "sr-1", SeriesMergeOrder = Order, StartsOnceMerged = ["#45", "#60"],
        })!;

        hold.Merges.Should().Be(TicketSeriesMergeHold.Needed(Order, hold.Names).Count + 1);
    }
}
