using AgentSmith.Tests.Prompts;
using FluentAssertions;

namespace AgentSmith.Tests.Skills;

/// <summary>
/// The rules of series 2026-10-14-dbce — ticket lists in merge order and absence criteria judged over the source
/// tree — read from the PINNED catalog. The write tools, checker, review and filing work without these sentences; a
/// release that drops one fails here instead of leaving a design partner that never proposes a list.
/// <para>
/// 2026-11-12-681f: v5.29.0 writes tickets with <c>write_ticket</c>; the count, the field bounds, the quoting and the
/// outcome block went with the fence, and the ticket's fields live in the tool's schema.
/// </para>
/// </summary>
public sealed class TicketGraphSkillRulesTests
{
    /// <summary>First agent-smith-skills release carrying every rule of the series.</summary>
    private static readonly Version TicketGraphRelease = new(5, 18, 0);

    private static string Because(string rule) =>
        $"the pin is {PackagedMaster.Pin} and {rule} ships from v{TicketGraphRelease}";

    private static string DesignPartner() => PackagedMaster.Folded("design-partner-master");

    // 2026-11-18-550ac: design-partner 3.5.0 cuts one ticket per theme; the merge test and the branch-state test are gone.
    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_OffersOneTicketPerTheme() =>
        DesignPartner().Should().Contain("`draft:tickets/<key>.yaml`, one per theme;", Because("the tickets outcome"))
            .And.NotContain("one per piece that must merge");

    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_CutsByThemeInsteadOfTheMergeTest()
    {
        var master = DesignPartner();

        master.Should().Contain("Cut the work the way a lead hands it to a team: one ticket per theme",
            "the cut is described as a lead hands work to a team")
            .And.Contain("One theme with one deliverable is a spec; one theme in ordered parts is an epic; several themes are tickets.",
                "the three shapes follow from the themes")
            .And.NotContain("The merge test first").And.NotContain("the branch-state test");
    }

    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_RequiresNameOnlyTicketsMergedFirstWithIdsWithoutHash()
    {
        var master = DesignPartner();

        master.Should().Contain("`requires` orders them, so a ticket's dependencies are written before it",
            Because("the merge order rule"));
        master.Should().Contain("A filed ticket is required as `ticket:<tracker id>` (`ticket:41`, `ticket:PROJ-41`)",
            Because("a filed ticket's id without '#'"));
    }

    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_TicketIsWrittenAgainstItsMergedRequiresAndSizedForOneRun() =>
        DesignPartner().Should().Contain(
            "Its requirement describes the change against the code as it stands once its requires have merged, "
            + "small enough for one run to build, review and merge; its specs state their facts against that state too.",
            Because("the ticket size rule"));

    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_ProposesTicketsOnlyUnbound() =>
        DesignPartner().Should().Contain("Tickets belong to a conversation not bound to a ticket.",
            Because("the unbound-only rule"));

    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_ScopesAbsenceCriteriaToTheSourceTree() =>
        DesignPartner().Should().Contain("An absence criterion reads \"no file outside .agentsmith/ contains X\"",
            Because("the scoped absence criterion"));

    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_SaysWhatATicketListFiles()
    {
        var master = DesignPartner();

        master.Should().Contain("**A ticket** carries its specs.", Because("a ticket carries its specs (2026-11-17-5701d)"));
        master.Should().Contain(
            "its specs under `draft:tickets/<key>/` are written like any spec and filed onto the "
            + "ticket's branch, so its run builds them.",
            Because("the filing sentence"));
        master.Should().Contain("`draft:tickets/<key>/<label>.yaml`", Because("where a ticket's specs are written"));
    }
}
