using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Validation;
using FluentAssertions;

namespace AgentSmith.Tests.Validation;

// p0400: a phase spec may declare ships_code: false — the phase ships knowledge
// instead of code. The schema accepts the field, the reader surfaces it on the
// draft, and its absence means the default: the phase ships code.
public sealed class PhaseShipsCodeTests
{
    private readonly SpecDraftValidator _validator = new(new PhaseSpecSchemaProvider());
    private readonly PhaseDraftReader _reader = new();
    private readonly DerivedPhaseYamlRenderer _renderer = new();

    private const string KnowledgePhaseYaml = """
        phase: p9901
        goal: "Inventory and classify every branch"
        steps:
          - id: classify
            action: "Classify each branch"
        done:
          - "Every branch is classified"
        ships_code: false
        """;

    [Fact]
    public void PhaseSchema_ShipsCodeFalse_ParsesAndDefaultsTrue()
    {
        _validator.ValidateYaml(KnowledgePhaseYaml).Should().BeOfType<SpecDraftValid>(
            "the schema accepts the ships_code declaration");
        _reader.Read(KnowledgePhaseYaml).ShipsCode.Should().BeFalse();

        var withoutDeclaration = _reader.Read("phase: p9902\ngoal: \"g\"\ndone:\n  - \"d\"\n");
        withoutDeclaration.ShipsCode.Should().BeTrue("absent means the default — a phase ships code");
    }

    [Fact]
    public void DerivedPhaseYaml_ShipsCodeFalse_RenderedAndSchemaValid()
    {
        var yaml = _renderer.Render(
            "p9903", "Inventory the branches", [], [("classify", "Classify each branch")],
            ["Every branch is classified"], "p9903-inventory.md", [1], "T-1", shipsCode: false);

        yaml.Should().Contain("ships_code: false");
        _validator.ValidateYaml(yaml).Should().BeOfType<SpecDraftValid>();
        _reader.Read(yaml).ShipsCode.Should().BeFalse();
    }

    [Fact]
    public void DerivedPhaseYaml_Default_StatesShipsCodeExplicitly()
    {
        var yaml = _renderer.Render(
            "p9904", "Change the code", [], [("impl", "Do it")],
            ["build green"], "p9904-change.md", [1], "T-1");

        // p0400b: an omitted default is unauditable — a spec with no field reads the
        // same whether the model declared true or never declared at all, which is
        // exactly the ambiguity that sent run 4599's diagnosis down the wrong path.
        yaml.Should().Contain("ships_code: true",
            "the deliverable is declared in both directions, so the declaration can be read back");
        _validator.ValidateYaml(yaml).Should().BeOfType<SpecDraftValid>();
        _reader.Read(yaml).ShipsCode.Should().BeTrue();
    }
}
