using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>p0439: the "Not delivered" section names every open phase and the one reason.</summary>
public sealed class ShortfallSectionTests
{
    [Fact]
    public void Build_NoShortfall_IsEmpty()
    {
        ShortfallSection.Build(null).Should().BeEmpty();
    }

    [Fact]
    public void Build_NamesEveryOpenPhaseInTheDerivationsWordsAndTheReason()
    {
        var shortfall = new RunShortfall(
            [new PhaseProgress("p0001a", "Introduce the guard", PhaseRunState.Done)],
            [
                new PhaseProgress("p0001b", "Move the callers", PhaseRunState.Failed, "dotnet test exited 1"),
                new PhaseProgress("p0001c", "Retire the old check", PhaseRunState.NotStarted),
            ],
            "per-pipeline cost budget exhausted");

        var section = ShortfallSection.Build(shortfall);

        section.Should().Contain(ShortfallSection.Heading)
            .And.Contain("1 of 3 phase(s) are built, verified and on this branch")
            .And.Contain("per-pipeline cost budget exhausted")
            .And.Contain("- **p0001b** — Move the callers (failed: dotnet test exited 1)")
            .And.Contain("- **p0001c** — Retire the old check (not started)")
            .And.NotContain("p0001a", "a delivered phase is not among the missing ones");
    }
}
