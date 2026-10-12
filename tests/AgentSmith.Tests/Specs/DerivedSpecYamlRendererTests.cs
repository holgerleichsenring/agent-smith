using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-10-02-3f06a: the renderer wrote facts, assumptions and contexts on the strength of an
/// open top level. Now that the schema declares their shapes, what it writes must meet them —
/// an invalid derived spec is refused, and the single-spec fallback throws.
/// </summary>
public sealed class DerivedSpecYamlRendererTests
{
    [Fact]
    public void DerivedSpecYamlRenderer_FactsAssumptionsAndContexts_ValidateAgainstTheSchema()
    {
        var facts = new SpecFacts(
            [new PhaseFact("one direct package is affected", "[L1] api: the derivation ran 'ls' exited 0")],
            ["every other finding is transitive", "42"]);

        var yaml = new DerivedSpecYamlRenderer().Render(
            "p19106a", "Raise the floors", [], [("raise", "Raise the versions")], ["The build exits 0."],
            "p19106a.md", [0], "19106", facts, ["frontend", "true"]);

        new SpecDraftValidator(new SpecSchemaProvider()).ValidateYaml(yaml)
            .Should().BeOfType<SpecDraftValid>(yaml);
        var draft = new SpecDraftReader().Read(yaml);
        draft.Assumptions.Should().Equal("every other finding is transitive", "42");
        draft.Contexts.Should().Equal("frontend", "true");
    }

    /// <summary>2026-10-06-03c7b: the derived spec names its id under the key the schema requires.</summary>
    [Fact]
    public void DerivedSpecYamlRenderer_Render_WritesSpecKey()
    {
        var yaml = new DerivedSpecYamlRenderer().Render(
            "p19106a", "Raise the floors", [], [("raise", "Raise the versions")], ["The build exits 0."],
            "p19106a.md", [0], "19106");

        yaml.Should().StartWith("spec: p19106a").And.NotContain("phase:");
        new SpecDraftReader().Read(yaml).PhaseId.Should().Be("p19106a");
    }
}
