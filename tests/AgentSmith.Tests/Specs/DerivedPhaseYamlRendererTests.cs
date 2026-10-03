using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-10-02-3f06a: the renderer wrote facts, assumptions and contexts on the strength of an
/// open top level. Now that the schema declares their shapes, what it writes must meet them —
/// an invalid derived phase is refused, and the single-phase fallback throws.
/// </summary>
public sealed class DerivedPhaseYamlRendererTests
{
    [Fact]
    public void DerivedPhaseYamlRenderer_FactsAssumptionsAndContexts_ValidateAgainstTheSchema()
    {
        var facts = new PhaseFacts(
            [new PhaseFact("one direct package is affected", "[L1] api: the derivation ran 'ls' exited 0")],
            ["every other finding is transitive", "42"]);

        var yaml = new DerivedPhaseYamlRenderer().Render(
            "p19106a", "Raise the floors", [], [("raise", "Raise the versions")], ["The build exits 0."],
            "p19106a.md", [0], "19106", facts, ["frontend", "true"]);

        new SpecDraftValidator(new PhaseSpecSchemaProvider()).ValidateYaml(yaml)
            .Should().BeOfType<SpecDraftValid>(yaml);
        var draft = new PhaseDraftReader().Read(yaml);
        draft.Assumptions.Should().Equal("every other finding is transitive", "42");
        draft.Contexts.Should().Equal("frontend", "true");
    }
}
