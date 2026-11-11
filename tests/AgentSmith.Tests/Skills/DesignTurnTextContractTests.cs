using AgentSmith.Tests.Prompts;
using FluentAssertions;

namespace AgentSmith.Tests.Skills;

/// <summary>
/// 2026-11-11-205f: the design-partner texts and the server agree, read from the PINNED catalog.
/// A release that brings back a contradiction the Fable review found fails here.
/// </summary>
public sealed class DesignTurnTextContractTests
{
    [Fact]
    public void OutcomeFormat_SpecAndEpic_ShowTheReposKey()
    {
        var spec = PackagedMaster.Reference("design-outcome-spec");
        var epic = PackagedMaster.Reference("design-outcome-epic");

        spec.Should().Contain("\nrepos: [",
            "DialogDraftRepos reads a top-level repos key, so the template has to show it");
        epic.Should().Contain("    repos: [",
            "every epic child is checked for its own repository");
        spec.Should().NotContain("Repositories, constraints and exclusions are PROSE",
            "a format that calls the repository prose contradicts the server's check");
    }
}
