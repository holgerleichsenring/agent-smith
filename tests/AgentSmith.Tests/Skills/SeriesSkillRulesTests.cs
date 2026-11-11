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
    private static string SpecFormat() => PackagedMaster.Reference("design-outcome-spec");
    private static string EpicFormat() => PackagedMaster.Reference("design-outcome-epic");
    private static string SpecDerivation() => PackagedMaster.Read("spec-derivation-master");
    private static string CodingAgent() => PackagedMaster.Read("coding-agent-master");

    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_AppliesReviewCutsAndReproposes() =>
        SpecFormat().Should().Contain(
            "A review may answer your spec, epic or tickets with cuts that quote your draft: apply exactly "
            + "those cuts, change nothing else, and re-propose the whole outcome.",
            $"the pin is {PackagedMaster.Pin} and the review round's cuts rule names tickets from v5.18.0");

    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_EmitsASeriesContract() =>
        EpicFormat().Should().Contain(
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
    public void EmbeddedSkills_DesignPartnerMaster_LeavesMockAuthoringToItsTool() =>
        // 2026-11-10-14a3h: the mock rule lives in write_mock's description, offered only with visual material.
        DesignPartner().Should().NotContain("**Authoring a mock.**", Because("the mock rule moved to its tool"));

    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_SaysTheSeriesAndAttemptSize()
    {
        var master = EpicFormat();

        // 2026-11-10-14a3h: the parser states the cap when a cut exceeds it; the format no longer announces it.
        master.Should().Contain("parent and two or more ordered children — each entry is a complete spec",
            Because("the epic size wording")).And.NotContain("series cap");
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

        // 2026-11-10-fc66: a design turn carries neither tool — its questions go in the reply.
        // 2026-11-10-d854a: each with a proposal, and state_open_questions lays them on the draft's card as well.
        // 2026-11-11-205fb: the draft is built on those proposals, never held back for them.
        DesignPartner().Should().Contain(
            "- Your questions go in your reply, after the analysis that raised\n  them, each with your proposal; "
            + "the operator answers in the next\n  message. When you draft, `state_open_questions` puts them on the"
            + "\n  draft's card as well, and the draft is built on your proposals,\n  marked proposed.",
            Because("the design partner's question rule"));
    }

    [Fact]
    public void EmbeddedSkills_DesignPartner_NamesNoRemovedTool()
    {
        foreach (var removed in new[] { "ask_human", "decide_by_default", "http_request", "list_directory", "grep_in_file" })
            DesignPartner().Should().NotContain($"`{removed}`", Because($"the design turn no longer carries {removed}"));
        DesignPartner().Should().Contain("`state_work_picture`: the kind of undertaking", Because("the kinds are the tool's"));
    }

    /// <summary>2026-11-10-14a3h: the unit of a child is named, and a proposed decision has its syntax.</summary>
    [Fact]
    public void EmbeddedSkills_DesignPartner_DefinesTheChildAndTheProposedPrefix()
    {
        DesignPartner().Should().Contain("A child is what one coding pass builds and one reviewer can check",
            Because("the unit of a child")).And.Contain("`proposed: <choice> —", Because("the proposed syntax"))
            .And.NotContain("those questions all at once", Because("one fork per question"));
        SpecFormat().Should().Contain("\"proposed: <a choice you make where they leave it open> — <what it rests on>\"",
            Because("the format shows a proposed decision"));
    }

    [Fact]
    public void EmbeddedSkills_SpecDerivationMaster_SaysWhatOneAttemptRuns() =>
        SpecDerivation().Should().Contain(
            "At most {MaxSpecs} specs — the most one attempt runs (a re-cut: the most the series holds); beyond that "
            + "the ticket is a programme and belongs in a design conversation.",
            Because("the attempt size sentence"));
}
