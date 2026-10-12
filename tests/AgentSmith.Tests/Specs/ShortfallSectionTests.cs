using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>p0439: the "Not delivered" section names every open spec and the one reason.</summary>
public sealed class ShortfallSectionTests
{
    [Fact]
    public void Build_NoShortfall_IsEmpty()
    {
        ShortfallSection.Build(null).Should().BeEmpty();
    }

    [Fact]
    public void Build_NamesEveryOpenSpecInTheDerivationsWordsAndTheReason()
    {
        var shortfall = new RunShortfall(
            [new SpecProgress("p0001a", "Introduce the guard", SpecRunState.Done)],
            [
                new SpecProgress("p0001b", "Move the callers", SpecRunState.Failed, "dotnet test exited 1"),
                new SpecProgress("p0001c", "Retire the old check", SpecRunState.NotStarted),
            ],
            "per-pipeline cost budget exhausted");

        var section = ShortfallSection.Build(shortfall);

        section.Should().Contain(ShortfallSection.Heading)
            .And.Contain("1 of 3 spec(s) are built, verified and on this branch")
            .And.Contain("per-pipeline cost budget exhausted")
            .And.Contain("- **p0001b** — Move the callers (failed: dotnet test exited 1)")
            .And.Contain("- **p0001c** — Retire the old check (not started)")
            .And.NotContain("p0001a", "a delivered spec is not among the missing ones");
    }

    [Fact]
    public void Build_SpecHandedBackOnAFalsePremise_IsNotDescribedAsNotStartedOrAsFailed()
    {
        // 2026-09-17-0e79c: the standing is its own value, and this switch's default arm says
        // "not started" — which of a spec the run stopped on would be plainly false.
        var shortfall = new RunShortfall(
            [],
            [new SpecProgress(
                "p0001b", "Move the callers", SpecRunState.HandedBack,
                "False premise in p0001b: \"three callers build their own check\" — [M1] …")],
            "a premise of p0001b no longer holds");

        var section = ShortfallSection.Build(shortfall);

        section.Should().Contain("handed back, never built")
            .And.Contain("False premise in p0001b")
            .And.NotContain("(not started)", "it was entered and stopped, not skipped")
            .And.NotContain("(failed:", "nothing was built, so nothing went red");
    }
}
