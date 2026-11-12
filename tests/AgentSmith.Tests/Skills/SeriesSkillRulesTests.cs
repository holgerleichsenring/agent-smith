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
/// <para>
/// 2026-11-12-681f: v5.29.0 folded the outcome references into the master and dropped the re-propose-after-review
/// rule; the design-partner assertions read the master's prose with whitespace folded.
/// </para>
/// </summary>
public sealed class SeriesSkillRulesTests
{
    /// <summary>First agent-smith-skills release carrying every rule of the series.</summary>
    private static readonly Version SeriesRulesRelease = new(5, 16, 0);

    private static string Because(string rule) =>
        $"the pin is {PackagedMaster.Pin} and {rule} ships from v{SeriesRulesRelease}";

    private static string DesignPartner() => PackagedMaster.Read("design-partner-master");
    private static string Folded() => PackagedMaster.Folded("design-partner-master");
    private static string SpecDerivation() => PackagedMaster.Read("spec-derivation-master");
    private static string CodingAgent() => PackagedMaster.Read("coding-agent-master");

    /// <summary>2026-11-12-681f: the contract's shape is write_contract's schema; the master names the tool.</summary>
    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_EmitsASeriesContract() =>
        Folded().Should().Contain("`write_contract` for the names and data the children share",
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

    /// <summary>2026-11-12-681f: no count bounds a proposal; a long epic files as several series on the server side.</summary>
    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_SetsNoSizeOnAProposal()
    {
        var master = Folded();

        master.Should().Contain("The proposal may be as large as the work", Because("the unbounded proposal"));
        master.Should().NotContain("series cap").And.NotContain("between two and eight").And.NotContain("2 to 20");
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

        // 2026-11-10-fc66: a design turn carries neither tool. 2026-11-12-681f: its questions go on
        // state_open_questions, the one channel, each with a proposal the draft is built on.
        var partner = Folded();
        partner.Should().Contain("Questions only the operator can answer go on `state_open_questions`, each with your proposal",
            Because("the design partner's question rule"));
        partner.Should().Contain("The proposal is built on your proposals, marked `proposed:`.",
            Because("the draft is never held back for a question"));
    }

    [Fact]
    public void EmbeddedSkills_DesignPartner_NamesNoRemovedTool()
    {
        foreach (var removed in new[]
                 { "ask_human", "decide_by_default", "http_request", "list_directory", "grep_in_file", "outcome_format", "note_reference" })
            DesignPartner().Should().NotContain($"`{removed}`", Because($"the design turn no longer carries {removed}"));
        Folded().Should().Contain("`state_work_picture` after your first reading pass: the kind of undertaking",
            Because("the kinds are the tool's"));
    }

    /// <summary>2026-11-10-14a3h: the unit of a child is named, and a proposed decision has its syntax.</summary>
    [Fact]
    public void EmbeddedSkills_DesignPartner_DefinesTheChildAndTheProposedPrefix()
    {
        Folded().Should().Contain("A child is what one coding pass builds and one reviewer can check",
            Because("the unit of a child"))
            .And.Contain("a choice you make starts with `proposed:` and says what it rests on", Because("the proposed syntax"))
            .And.NotContain("those questions all at once", Because("one fork per question"));
    }

    [Fact]
    public void EmbeddedSkills_SpecDerivationMaster_SaysWhatOneAttemptRuns() =>
        SpecDerivation().Should().Contain(
            "At most {MaxSpecs} specs — the most one attempt runs (a re-cut: the most the series holds); beyond that "
            + "the ticket is a programme and belongs in a design conversation.",
            Because("the attempt size sentence"));
}
