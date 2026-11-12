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

    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_OffersTicketsAtMergeBoundaries() =>
        DesignPartner().Should().Contain("`write_ticket`, one per piece that must merge before another can be built",
            Because("the tickets outcome"));

    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_AsksTheMergeTestThenTheBranchStateTest()
    {
        var master = DesignPartner();
        const string mergeTest = "The merge test first: must one piece be MERGED before the next can be built or reviewed?";
        const string branchTest = "Then the branch-state test: can ONE branch state satisfy every claim at once?";

        master.Should().Contain(mergeTest, Because("the merge test")).And.Contain(branchTest, Because("the branch-state test"));
        master.IndexOf(mergeTest, StringComparison.Ordinal).Should()
            .BeLessThan(master.IndexOf(branchTest, StringComparison.Ordinal), "the merge test is asked first");
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
            + "small enough for one run to build, review and merge.",
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
    public void EmbeddedSkills_CodingAgentMaster_JudgesAbsenceOverTheSourceTree() =>
        PackagedMaster.Read("coding-agent-master").Should().Contain(
            "Judge an absence criterion (\"no file contains X\") over the source tree: a match under .agentsmith/ "
            + "does not count against it, because the committed spec there quotes X.",
            Because("the absence judgement"));

    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_SaysWhatATicketListFiles()
    {
        var master = DesignPartner();

        master.Should().Contain("**A ticket** is a ticket text: the run it starts derives its own specs.",
            Because("a ticket carries no spec"));
        master.Should().Contain(
            "A ticket waits until its requires merge; to settle its specs earlier, the operator opens Work it out on it.",
            Because("the filing sentence"));
    }
}
