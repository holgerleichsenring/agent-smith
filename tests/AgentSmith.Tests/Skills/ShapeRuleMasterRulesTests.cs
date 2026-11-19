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
        var master = PackagedMaster.Folded("design-partner-master");

        master.Should().Contain("A shape rule (size, layout, naming) is a step, not a done line.", // 2026-11-12-681f: in the master
            "a shape rule stays out of done");
    }

}
