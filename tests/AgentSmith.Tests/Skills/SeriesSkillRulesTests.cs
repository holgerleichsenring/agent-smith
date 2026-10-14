using AgentSmith.Tests.Prompts;
using FluentAssertions;

namespace AgentSmith.Tests.Skills;

/// <summary>
/// The rules series 2026-10-13-8762 put into the masters, read from the PINNED catalog.
/// <para>
/// Each assertion quotes the exact sentence its member named; the tools those sentences
/// point at work without them, so a release that drops one fails here instead of leaving
/// a live run that never asks for the tool.
/// </para>
/// </summary>
public sealed class SeriesSkillRulesTests
{
    /// <summary>First agent-smith-skills release carrying every rule of the series.</summary>
    private static readonly Version SeriesRulesRelease = new(5, 16, 0);

    private static string Because(string rule) =>
        $"the pin is {PackagedMaster.Pin} and {rule} ships from v{SeriesRulesRelease}";

    private static string DesignPartner() => PackagedMaster.Read("design-partner-master");
    private static string SpecDerivation() => PackagedMaster.Read("spec-derivation-master");
    private static string CodingAgent() => PackagedMaster.Read("coding-agent-master");

    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_AppliesReviewCutsAndReproposes() =>
        DesignPartner().Should().Contain(
            "A review may answer your spec or epic with cuts that quote your draft: apply exactly those cuts, "
            + "change nothing else, and re-propose the whole outcome.",
            Because("the review round's cuts rule"));

    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_EmitsASeriesContract() =>
        DesignPartner().Should().Contain(
            "An epic may carry a top-level `contract:` with `names`, `data`, `areas` and `budgets`, naming each child "
            + "by its id or position; kind is one of event, system-event, table, context-key, endpoint, config, "
            + "command, tool, type; give each name or datum one owner (the others are writers), each number one "
            + "name per kind, no '*' in a path, and a line budget from every child that touches a shared file.",
            Because("the epic contract rule"));

    [Fact]
    public void EmbeddedSkills_SpecDerivationMaster_EmitsASeriesContract() =>
        SpecDerivation().Should().Contain(
            "Optionally add a \"contract\" object beside \"specs\" — \"names\" [{name, kind, number?, owner, writers?}], "
            + "\"data\" [{datum, lives_in, owner, writers?}], \"areas\" [{spec, paths}], \"budgets\" [{path, spec, max_lines}] "
            + "— naming each spec by its 1-based position in your reply (only an executed spec may be named by its id); "
            + "kind is one of event, system-event, table, context-key, endpoint, config, command, tool, type; give each "
            + "name or datum one owner (the others are writers), each number one name per kind, no '*' in a path, and a "
            + "line budget from every spec that touches a shared file.",
            Because("the derived set's contract rule"));

    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_AuthorsMocksWithWriteMock() =>
        DesignPartner().Should().Contain(
            "**Authoring a mock.** When the question is how a page should look, write the whole page with "
            + "`write_mock(spec_id, html)` — CSS and script inline, at most 90000 characters — and the operator sees "
            + "its desktop screenshot in the conversation as that spec's next version. Say which version you showed "
            + "and ask what to change. Before revising, `read_mock(spec_id)` returns the newest version's HTML; write "
            + "the whole page again as the next version, never a fragment.",
            Because("the mock authoring rule"));

    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_SaysTheSeriesAndAttemptSize()
    {
        var master = DesignPartner();

        master.Should().Contain(
            "parent and two or more ordered children (the epic parser states the series cap when a cut exceeds it) —",
            Because("the epic size wording"));
        master.Should().NotContain("between two and eight ordered children", "the series cap is configured, not eight");
    }

    [Fact]
    public void EmbeddedSkills_CodingAgentMaster_EndsEachSpecWithHandForward() =>
        CodingAgent().Should().Contain(
            "Before your verdict, call `hand_forward` once with what this spec renamed, moved, persisted and deviated "
            + "from — each renamed or moved entry as `<old> -> <new>` — so the next spec's master starts from it.",
            Because("the hand-forward rule"));

    [Fact]
    public void EmbeddedSkills_Masters_TakeAChoiceNeedingNoPersonByDefault()
    {
        var coding = CodingAgent();
        coding.Should().Contain(
            "Otherwise, use `decide_by_default` for a choice a person might overrule, `log_decision` for an "
            + "implementation choice.",
            Because("the coding master's default-choice rule"));
        coding.Should().Contain(
            "- Prefer decide_by_default (a choice a person might overrule) or log_decision (an implementation "
            + "choice) over asking the human.",
            Because("the coding master's default-choice rule"));

        DesignPartner().Should().Contain(
            "- `decide_by_default` is for a choice a person might overrule but need not make first: it returns at "
            + "once and the choice waits for a person to ratify it. A choice that needs a person before work "
            + "continues stays `ask_human`.",
            Because("the design partner's default-choice rule"));
    }

    [Fact]
    public void EmbeddedSkills_SpecDerivationMaster_SaysWhatOneAttemptRuns() =>
        SpecDerivation().Should().Contain(
            "At most {MaxSpecs} specs — the most one attempt runs (a re-cut: the most the series holds); beyond that "
            + "the ticket is a programme and belongs in a design conversation.",
            Because("the attempt size sentence"));
}
