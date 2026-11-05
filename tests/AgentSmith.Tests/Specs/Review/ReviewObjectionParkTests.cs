using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using FluentAssertions;
using static AgentSmith.Tests.Specs.Review.ReviewLoopRig;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>
/// 2026-11-02-2b26d: an inline-ticket run asks in the terminal and has no ticket to park a later spec on, so an
/// objection on any spec is asked before any spec is built — never through a parking sequence stop.
/// </summary>
public sealed class ReviewObjectionParkTests
{
    [Fact]
    public async Task InlineRun_LaterSpecObjected_AsksBeforeAnySpecIsBuilt()
    {
        var rig = new ReviewLoopRig(ReviewExhaustionTests.ThreeSpecs()).Answers(Build(), Build(), Refuse());
        rig.Pipeline.Set(ContextKeys.InlineTicket, new InlineTicket("Fix the boundary", "Totals of 100.00 get it."));

        var result = await rig.ExecuteAsync(Settings(share: 100));

        result.InsertNext.Should().ContainSingle().Which.Name.Should().Be(CommandNames.SpecHandback);
        result.Message.Should().Contain("parked before any spec is built");
        rig.Pipeline.Has(ContextKeys.SpecSequenceStop).Should().BeFalse("an inline run has no ticket to park on");
    }
}
