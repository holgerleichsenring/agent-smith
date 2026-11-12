using AgentSmith.Tests.Prompts;
using FluentAssertions;

namespace AgentSmith.Tests.Skills;

/// <summary>
/// 2026-11-11-205f: the design-partner texts and the server agree, read from the PINNED catalog.
/// A release that brings back a contradiction the Fable review found fails here.
/// <para>
/// 2026-11-12-681f: v5.29.0 carries no outcome references; the `repos` key the scope check reads is pinned on
/// write_spec's own schema (ProposalWriteToolsTests), and the master says one repository per spec.
/// </para>
/// </summary>
public sealed class DesignTurnTextContractTests
{
    private static string Folded() => PackagedMaster.Folded("design-partner-master");

    [Fact]
    public void SkillText_DesignPartner_GivesEachSpecOneRepository()
    {
        var master = Folded();

        master.Should().Contain("the one repository it changes", "every spec is checked for its own repository");
        master.Should().Contain("one repository per spec, several specs per repository",
            "a spec naming several repositories is refused with the epic split advice");
        master.Should().NotContain("Repositories, constraints and exclusions are PROSE",
            "a text that calls the repository prose contradicts the server's check");
    }

    [Fact]
    public void SkillText_DesignPartner_HasNoAskThenDraftRule()
    {
        var master = Folded();

        master.Should().NotContain("draft once it is answered",
            "a draft held back for a question costs a turn; the fork goes on state_open_questions instead");
        master.Should().NotContain("ask, rather than propose",
            "an unsettled done list is a proposal on the card, not a reason to hold the child back");
        master.Should().Contain("a turn without a proposal costs a turn", "the one rule says why the draft comes first");
        master.Should().Contain("Questions only the operator can answer go on `state_open_questions`",
            "the one rule says where the forks go");
    }

    /// <summary>2026-11-11-f874: the text carries drafting facts only, and the bound-ticket section no reply script.</summary>
    [Fact]
    public void SkillText_DesignPartner_HasNoRunLoreAndNoScriptedFirstReply()
    {
        var master = Folded();

        master.Should().NotContain("never rewrites the spec", "how a run later corrects a spec is not drafting knowledge");
        master.Should().Contain("re-checked against the code before it is built", "the re-check's effect on facts stays");
        master.Should().NotContain("Your first reply is an",
            "the bound-ticket section states the situation, not the order of replies").And
            .NotContain("a bound ticket's first judgement",
                "f874 review: the judgement lands as a proposal, so the answer outcome no longer names it");
    }

    [Fact]
    public void SkillText_DesignPartner_RoleMatchesTheWorkClone()
    {
        var master = Folded();

        master.Should().NotContain("what you run, you run in an upload's own container",
            "the work clone runs commands too (4778); a role that denies it stops the model trying things out");
        master.Should().Contain("you try in throwaway clones and upload containers",
            "the role names both places the turn may run something");
        master.Should().NotContain("When `remember` is on your surface",
            "the remember tool describes itself; the skill does not restate it");
    }
}
