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
    public void SpecRegisters_ReadsAllSevenRegisters()
    {
        var registers = SpecRegisters.Read(Yaml);

        registers.Holds(SpecRegisterNames.Goal, "every order of the day").Should().BeTrue();
        registers.Entries(SpecRegisterNames.Scope).Should().Equal(
            "The export handler and its file writer.", "The import side stays as it is.");
        registers.Holds(SpecRegisterNames.Decisions, "keeps the archive readable").Should().BeTrue();
        registers.Entries(SpecRegisterNames.Steps).Should().Equal(
            "writer", "Write the orders through the buffered file writer.");
        registers.Entries(SpecRegisterNames.Tests).Should().Equal("Export_WritesEveryOrderOfTheDay");
        registers.Holds(SpecRegisterNames.Done, "lands in the export file").Should().BeTrue();
        registers.Entries(SpecRegisterNames.Facts).Should().Equal(
            "The export handler reads the orders from the order store.", "src/Export/ExportHandler.cs:12");
        registers.Holds(SpecRegisterNames.Goal, "lands in the export file").Should().BeFalse("that run is the done entry's");
    }

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

    [Fact]
    public void DerivationLookTerms_AttackReviewFor_IsCitedPathsPlusTwelveNeverBelow24()
    {
        DerivationLookTerms.AttackReviewFor(Draft()).Allowance.Should().Be(24, "one cited path plus 12 is below the floor");
        var wide = Draft() with
        {
            Facts = [.. Enumerable.Range(1, 20).Select(i => new Contracts.Models.SpecFact($"claim {i}", $"src/F{i}.cs:1, src/F{i}.cs:2"))],
        };
        DerivationLookTerms.AttackReviewFor(wide).Allowance.Should().Be(32, "twenty distinct paths plus 12");
        (DerivationLookTerms.AttackReview.EvidencePrefix, DerivationLookTerms.AttackReview.NumberedReads).Should().Be(("A", true));
    }

    private static SpecSet Set(IReadOnlyList<string> executed) =>
        new("k", [Spec("2026-10-14-1234a"), Spec("2026-10-14-1234b")], SpecAccounting.Empty, [], SpecSource.Derived)
        {
            Executed = executed,
        };

    private static DerivedSpec Spec(string id) => new(Draft() with { SpecId = id }, "x", "", []);
}
