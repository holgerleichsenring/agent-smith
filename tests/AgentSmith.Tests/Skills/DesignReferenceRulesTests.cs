using AgentSmith.Tests.Prompts;
using FluentAssertions;

namespace AgentSmith.Tests.Skills;

/// <summary>
/// 2026-10-01-aeb6d: the rules skills PR #205 put into the masters, read from the PINNED catalog.
/// <para>
/// The text is authored in agent-smith-skills (phases 2026-10-01-aeb6b, aeb6c, f5c3b, 7f7af and
/// 283dj); this repo holds it only through <c>SkillsCatalogVersion</c>. The framework sides of
/// those phases — the document fence, the subject binding, scenario criteria, bug criteria,
/// <c>design_read</c> and the reference renderer — each assume the master knows the rule. A
/// release that drops one fails here rather than on a live run.
/// </para>
/// </summary>
public sealed class DesignReferenceRulesTests
{
    /// <summary>First agent-smith-skills release carrying every rule below (skills PR #205).</summary>
    private static readonly Version RulesRelease = new(5, 8, 0);

    private static string DesignPartner() => PackagedMaster.Read("design-partner-master");

    private static string CodingAgent() => PackagedMaster.Read("coding-agent-master");

    private static string Because(string why) =>
        $"the pin is {PackagedMaster.Pin} and this rule ships from v{RulesRelease}; {why}";

    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_WritesDocumentsWithoutDiscussion()
    {
        var master = DesignPartner();

        master.Should().Contain(
            "- **document** (the operator asks for a text to take elsewhere",
            Because("a document is an outcome of its own, not a phase or an answer"));
        master.Should().Contain(
            "A request for a document is not a request for work: it is answered with",
            Because("the discuss-first gate would otherwise spend a turn before the text"));
        master.Should().Contain(
            "the document in the first reply, with no discussion before it.",
            "the rule has to say WHEN the document comes, or it waits behind a discussion");
        master.Should().Contain("{{ref:writing-documents}}",
            "the fence and the shape live in the shared reference the master pulls in");

        var reference = PackagedMaster.ReadEntry("references/writing-documents.md");
        reference.Should().Contain("The whole document stands inside ONE fence of four",
            "the dialog extracts the document by that fence; a three-backtick fence collides "
            + "with the code inside it");
        reference.Should().Contain("A document is read by someone who did not see this conversation",
            "a document written for the chat is useless to the next session it is handed to");
    }

    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_StaysOnItsProjectAndTicket()
    {
        var master = DesignPartner();

        master.Should().Contain("### What you work on",
            Because("the master needs its subject named before it can stay on it"));
        master.Should().Contain(
            "A question about neither — the weather, the news, a recipe — gets one",
            "the binding has to say what an off-subject question gets instead of an answer");
        master.Should().Contain(
            "It gets no tool call and no answer from your own knowledge; this",
            "an off-subject question must cost neither a tool call nor an ungrounded answer");
        master.Should().Contain(
            "comes before the grounding tiers below. A request for a document is",
            "a document request is bound to the same subject, or it becomes the way around it");
    }

    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_WritesScenarioCriteria()
    {
        var master = DesignPartner();

        master.Should().Contain("    when: \"<the trigger>\"",
            Because("the draft template has to show the scenario shape the schema accepts"));
        master.Should().Contain("    then: \"<the observable result>\"",
            "a scenario without its observable result is not verifiable");
        master.Should().Contain(
            "- A `done` item is one line, or a scenario when the criterion has a",
            "the rule says when a scenario is the right shape, so a line stays a line");
        master.Should().Contain(
            "scenario as the line `GIVEN … WHEN … THEN …`, so write each part as",
            "every reader renders the scenario as one line, so each part must read as a clause");
    }

    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_ListsBugCriteria()
    {
        var master = DesignPartner();

        master.Should().Contain(
            "acceptance_criteria:          # optional: how the fix is verified, one item each",
            Because("a fix-bug ticket's criteria are a list the ticket renders item by item"));
        master.Should().Contain(
            "  - \"WHEN <the trigger> THEN <the observable result>\"",
            "the bug template shows a scenario criterion as well as a plain one");
    }

    [Fact]
    public void EmbeddedSkills_Masters_DescribeDesignRead()
    {
        var partner = DesignPartner();
        var coding = CodingAgent();

        partner.Should().Contain("## Reading a design",
            Because("design_read is on the design partner's surface when a Figma source exists"));
        partner.Should().Contain("`design_read` is the way to a design: a",
            "a Figma page fetched any other way returns the application shell");
        partner.Should().Contain("  `version` design_read reported",
            "a ticket derived from a design cites the version the run compares against");

        coding.Should().Contain(
            "A **Figma design** the ticket cites is requirement data too, and `design_read`",
            Because("the coding master reads the cited frames itself"));
        coding.Should().Contain(
            "link, pass it as `expected_version`: the answer opens `design unchanged` or",
            "the run reports whether the design moved since the ticket cited it");
        coding.Should().Contain("cannot read is a blocker to name, not a design to guess.",
            "a failed read must stop the guess, not license it");
        coding.Should().Contain("a figma.com link is read with `design_read`, never `web_fetch`.",
            "web_fetch on a Figma link returns the shell, which reads as an empty design");
    }

    [Fact]
    public void EmbeddedSkills_Masters_BuildAgainstVisualReferences()
    {
        var partner = DesignPartner();
        var coding = CodingAgent();
        var precedence = PackagedMaster.ReadEntry("references/source-precedence.md");

        partner.Should().Contain("## Reading a website, a mock or a design system",
            Because("uploads, mocks and DESIGN.md reach the design partner as exact values"));
        partner.Should().Contain(
            "the `DESIGN.md` token — never \"looks like the reference\". A run compares",
            "criteria carry the values, because the comparison reports and never decides");
        partner.Should().Contain(
            "inherit — while the look of a visual reference is part of that WHAT.",
            "the precedence preamble must not read a visual reference's look as mere form");

        coding.Should().Contain(
            "A **visual reference** is requirement data the same way, and each form gives",
            Because("the coding master builds against the reference the approval carries"));
        coding.Should().Contain(
            "`compare_reference` is on your surface, compare what you built with the",
            "the run compares its page with the reference once the build is green");
        coding.Should().Contain(
            "in result.md under \"Visual comparison\"; it reports and never decides the run —",
            "the comparison is evidence, while the verdict stays with the acceptance contract");

        precedence.Should().Contain(
            "**look is part of the WHAT**: its colours, type, spacing, radii and shadows,",
            "source precedence rule 4 makes a visual reference's look a requirement");
        precedence.Should().Contain(
            "layout, framework — are form and carry no authority: build the look with",
            "its markup and stylesheet structure stay form, which rule 4 denies authority");
    }
}
