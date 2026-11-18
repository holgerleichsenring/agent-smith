using AgentSmith.Tests.Prompts;
using FluentAssertions;

namespace AgentSmith.Tests.Skills;

/// <summary>
/// 2026-10-01-aeb6d: the rules skills PR #205 put into the masters, read from the PINNED catalog.
/// <para>
/// The text is authored in agent-smith-skills (specs 2026-10-01-aeb6b, aeb6c, f5c3b, 7f7af and
/// 283dj); this repo holds it only through <c>SkillsCatalogVersion</c>. The framework sides of
/// those specs — the document fence, the subject binding, scenario criteria, bug criteria,
/// <c>design_read</c> and the reference renderer — each assume the master knows the rule. A
/// release that drops one fails here rather than on a live run.
/// </para>
/// <para>
/// 2026-11-05-862ec: the draft templates moved into the design-outcome references, and
/// outcome_format('document') serves writing-documents in place of the master's citation.
/// </para>
/// <para>
/// 2026-11-12-681f: v5.29.0 folded the references back into the master; the design-partner rules read its prose.
/// </para>
/// </summary>
public sealed class DesignReferenceRulesTests
{
    /// <summary>First agent-smith-skills release carrying every rule below (skills PR #205).</summary>
    private static readonly Version RulesRelease = new(5, 8, 0);

    private static string DesignPartner() => PackagedMaster.Read("design-partner-master");

    private static string Folded() => PackagedMaster.Folded("design-partner-master");

    private static string CodingAgent() => PackagedMaster.Folded("coding-agent-master");

    private static string Because(string why) =>
        $"the pin is {PackagedMaster.Pin} and this rule ships from v{RulesRelease}; {why}";

    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_WritesDocumentsWithoutDiscussion()
    {
        var master = Folded();

        master.Should().Contain(
            "A request for a document is answered with the document in the first reply, inside one fence of four backticks",
            Because("the dialog extracts the document by that fence, in the first reply rather than behind a discussion"));
        master.Should().Contain("for a reader who did not see this chat",
            "a document written for the chat is useless to the next session it is handed to");
        master.Should().Contain("its first line a `#` heading the operator sees as its title",
            "the document's title is read off its first heading");
        master.Should().NotContain("{{ref:writing-documents}}", "2026-11-12-681f: the rule lives in the master");
    }

    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_StaysOnItsProjectAndTicket()
    {
        var master = Folded();

        master.Should().Contain("Your subject is the project whose context, code map and repositories are above",
            Because("the master needs its subject named before it can stay on it"));
        master.Should().Contain("when the conversation is bound to a ticket, that ticket",
            "a bound conversation's subject includes its ticket");
        master.Should().Contain("A question about something else gets one sentence that names what you work on",
            "the binding has to say what an off-subject question gets instead of an answer");
    }

    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_WritesScenarioCriteria()
    {
        var master = Folded();

        // 2026-11-18-550ac: design-partner 3.5.0 — one claim per done line, and a rule with several cases names them.
        master.Should().Contain("Each `done` line is one claim a reviewer answers yes or no against the code",
            Because("a done line is one verifiable claim"));
        master.Should().Contain("a rule with several cases names them", "a bundled rule is spelled out case by case");
        master.Should().Contain("a scenario `when`/`then` (`given` when the start matters)",
            "the master names the scenario shape the schema accepts");
    }

    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_ListsBugCriteria() =>
        Folded().Should().Contain(
            "**A bug** says what is wrong, where (file or method when you saw it) and what correct behaviour looks like",
            Because("a fix-bug ticket is executed as it stands, so it carries how the fix is verified"));

    [Fact]
    public void EmbeddedSkills_Masters_DescribeDesignRead()
    {
        var partner = DesignPartner();
        var coding = CodingAgent();

        // 2026-11-10-14a3h: design_read's guidance rides its own description, offered only with a Figma source.
        partner.Should().NotContain("## Reading a design",
            Because("the section left the system prompt for the tool that appears with its material"));
        Folded().Should().Contain("citing the frame's link with its `node-id` and the `version` `design_read` reported",
            "a ticket derived from a design cites the version the run compares against");

        // 2026-11-18-4f3e: coding-agent-master 3.0.0 keeps the tool split in one sentence; the expected_version
        // and unreadable-frame rules left with the master that built by itself.
        coding.Should().Contain("Figma links are read with `design_read`, other web pages with `web_fetch`",
            Because("web_fetch on a Figma link returns the shell, which reads as an empty design"));
    }

    [Fact]
    public void EmbeddedSkills_Masters_BuildAgainstVisualReferences()
    {
        var partner = DesignPartner();
        var coding = CodingAgent();
        var precedence = PackagedMaster.ReadEntry("references/source-precedence.md");

        // 2026-11-10-14a3h: uploads keep their section; mock and design-system guidance ride their tools and sections.
        partner.Should().Contain("**Uploads.**",
            Because("an upload reaches the design partner as an address it reads like a repository"));
        Folded().Should().Contain("**Built against an upload**: name it and its `reference:` address")
            .And.Contain("criteria in the values read (sizes, spacing, hex colours, fonts, text)",
                "criteria carry the values, because the comparison reports and never decides");
        partner.Should().Contain("The material gives the WHAT",
            "the WHAT rule leads the precedence for this master");
        precedence.Should().Contain("**look is part of the WHAT**",
            "the visual-reference paragraph keeps one home, the shared order");

        // 2026-11-18-4f3e: coding-agent-master 3.0.0 names where uploads sit, reads the order from the shared
        // reference and looks at a page through render_reference; the compare_reference paragraph left with 2.1.0.
        coding.Should().Contain("uploaded material sits under `<repo>/.agentsmith/reference/<setId>/`",
            Because("the coding master builds against the reference the approval carries"));
        coding.Should().Contain("{{ref:source-precedence}}", "the look-is-the-WHAT order reaches it from one home");
        coding.Should().Contain("a page through `render_reference`", "the master looks at what was built");

        precedence.Should().Contain(
            "**look is part of the WHAT**: its colours, type, spacing, radii and shadows,",
            "source precedence rule 4 makes a visual reference's look a requirement");
        precedence.Should().Contain(
            "layout, framework — are form and carry no authority: build the look with",
            "its markup and stylesheet structure stay form, which rule 4 denies authority");
    }
}
