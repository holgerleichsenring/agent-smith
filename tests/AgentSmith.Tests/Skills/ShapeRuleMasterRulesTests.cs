using AgentSmith.Tests.Prompts;
using FluentAssertions;

namespace AgentSmith.Tests.Skills;

/// <summary>
/// 2026-10-12-fbbdc: the pinned design and derivation masters keep a rule about the code's form
/// out of a spec's done list, and draw the done list from an explicit 'Done when'.
/// </summary>
public sealed class ShapeRuleMasterRulesTests
{
    [Fact]
    public void PackagedMaster_DesignPartner_KeepsShapeRulesOutOfDone()
    {
        var format = PackagedMaster.Reference("design-outcome-spec");

        format.Should().Contain("is a step, never a done line, also when the operator wrote it", // 2026-11-10-14a3h: one sentence
            "a shape rule stays out of done");
    }

    [Fact]
    public void PackagedMaster_SpecDerivation_KeepsShapeRulesOutOfDone()
    {
        var master = PackagedMaster.Read("spec-derivation-master");

        master.Should().Contain("A SHAPE RULE IS NEVER A DONE CRITERION.");
        master.Should().Contain("THE TICKET'S OWN \"DONE WHEN\" IS THE DONE LIST.");
    }
}
