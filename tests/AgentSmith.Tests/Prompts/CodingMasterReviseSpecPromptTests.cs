using FluentAssertions;

namespace AgentSmith.Tests.Prompts;

/// <summary>
/// 2026-11-17-d0e0: the pinned coding master (2.1.0, skills v5.34.0) describes revise_spec, the tool
/// 2026-11-17-ec48a gives a spec's coding master — a changed spec is revised with one review, not named in
/// the verdict and left for a new spec.
/// </summary>
public sealed class CodingMasterReviseSpecPromptTests
{
    [Fact]
    public void PackagedCodingMaster_RevisesTheSpecItIsBuildingWithReviseSpec()
    {
        var master = PackagedMaster.Read("coding-agent-master");

        master.Should().Contain("revise it with `revise_spec`", "a changed spec is revised, not left to a verdict line");
        master.Should().Contain("A spec that already ran is never revised: correcting one\nis a new spec.",
            "only the spec being built is revised");
        master.Should().Contain("- `revise_spec` revises the spec you are building, on the ticket branch,",
            "the tool's own entry under Asking and deciding");
        master.Should().Contain("restructure your checklist\n  with `update_progress`",
            "an accepted revision's done criteria are the ones the master verifies");
        master.Should().NotContain("say so in the verdict", "the 2.0.1 rule this release replaces");
    }
}
