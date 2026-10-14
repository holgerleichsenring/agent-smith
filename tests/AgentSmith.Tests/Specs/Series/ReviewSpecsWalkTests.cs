using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.Specs.Review;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Specs.Series;

/// <summary>2026-10-13-8762u: the run-time review walks only what this attempt can build — never a spec the chunk
/// defers, whose review would be spent and then repeated next attempt.</summary>
public sealed class ReviewSpecsWalkTests
{
    [Fact]
    public async Task ReviewSpecs_TwentyFourSpecTail_WalksOnlySpecsPerAttempt()
    {
        var ids = Enumerable.Range(0, 24).Select(i => $"2026-10-14-5678{(char)('a' + i)}").ToList();
        var rig = new ReviewLoopRig(new SpecSet("shop-42",
            [.. ids.Select(id => ReviewLoopRig.Spec(CutRevisionFixtures.Draft(
                CutRevisionFixtures.With("spec: 2026-10-14-5678a", $"spec: {id}")) with { Repos = ["shop"] }))],
            SpecAccounting.Empty, [new SpecRevision(1, "initial derivation", DateTimeOffset.UtcNow)], SpecSource.Derived));

        var review = new Mock<ISpecReviewSettingsReader>();
        review.Setup(r => r.Current()).Returns(ReviewLoopRig.Settings(rounds: 1));
        using var _ = rig.RunContext.BeginScope(ReviewLoopRig.RunId);

        var result = await rig.Handler(review.Object,
                SeriesSettingsDoubles.Reader(new SeriesConfig { SpecsPerAttempt = 3 }))
            .ExecuteAsync(new Application.Models.ReviewSpecsContext(null, [], new AgentConfig(), rig.Pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Message);
        rig.Inputs.Select(i => i.Draft.SpecId).Distinct().Should().Equal(ids.Take(3));
    }
}
