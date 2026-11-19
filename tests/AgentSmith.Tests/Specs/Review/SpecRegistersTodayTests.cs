using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Specs;
using FluentAssertions;
using static AgentSmith.Tests.Specs.Review.AttackReviewFixtures;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>2026-10-13-8762e: a spec's registers as a cut quotes them, whether it describes today, and the
/// attack review's allowance.</summary>
public sealed class SpecRegistersTodayTests
{

    [Fact]
    public void SpecToday_RequiresAnUnbuiltSpec_DoesNotDescribeToday()
    {
        var draft = Draft("2026-10-14-1234b");

        SpecToday.Describes(draft, Set(executed: [])).Should().BeFalse();
        SpecToday.Describes(draft, Set(executed: ["2026-10-14-1234b"])).Should().BeTrue("a built member counts");
    }

    [Fact]
    public void SpecToday_RequiresASpecOutsideTheSet_DescribesToday()
    {
        SpecToday.Describes(Draft("2026-09-19-c511c"), Set(executed: [])).Should().BeTrue();
    }

    private static SpecSet Set(IReadOnlyList<string> executed) =>
        new("k", [Spec("2026-10-14-1234a"), Spec("2026-10-14-1234b")], SpecAccounting.Empty, [], SpecSource.Derived)
        {
            Executed = executed,
        };

    private static DerivedSpec Spec(string id) => new(Draft() with { SpecId = id }, "x", "", []);
}
