using AgentSmith.Tests.Prompts;
using FluentAssertions;

namespace AgentSmith.Tests.Skills;

/// <summary>
/// 2026-11-11-205f: the design-partner texts and the server agree, read from the PINNED catalog.
/// A release that brings back a contradiction the Fable review found fails here.
/// </summary>
public sealed class DesignTurnTextContractTests
{
    [Fact]
    public void OutcomeFormat_SpecAndEpic_ShowTheReposKey()
    {
        var spec = PackagedMaster.Reference("design-outcome-spec");
        var epic = PackagedMaster.Reference("design-outcome-epic");

        spec.Should().Contain("\nrepos: [",
            "DialogDraftRepos reads a top-level repos key, so the template has to show it");
        epic.Should().Contain("    repos: [",
            "every epic child is checked for its own repository");
        spec.Should().NotContain("Repositories, constraints and exclusions are PROSE",
            "a format that calls the repository prose contradicts the server's check");
    }

    [Fact]
    public void SkillText_DesignPartner_HasNoAskThenDraftRule()
    {
        var master = PackagedMaster.Read("design-partner-master");
        var epic = PackagedMaster.Reference("design-outcome-epic");

        master.Should().NotContain("draft once it is answered",
            "a draft held back for a question costs a turn; the fork goes on state_open_questions instead");
        epic.Should().NotContain("ask, rather than propose",
            "an unsettled done list is a proposal on the card, not a reason to hold the child back");
        master.Should().Contain("lay the forks on `state_open_questions`",
            "the one rule says where the forks go");
    }

    /// <summary>2026-11-11-f874: the format carries drafting facts only, and the bound-ticket section no reply script.</summary>
    [Fact]
    public void SkillText_DesignPartner_HasNoRunLoreAndNoScriptedFirstReply()
    {
        PackagedMaster.Reference("design-outcome-spec").Should().NotContain("never rewrites the spec",
            "how a run later corrects a spec is not drafting knowledge").And
            .Contain("stops the spec before it spends a token", "the re-check's effect on facts stays");
        PackagedMaster.Read("design-partner-master").Should().NotContain("Your first reply\nis an",
            "the bound-ticket section states the situation, not the order of replies");
    }

    [Fact]
    public void SkillText_DesignPartner_RoleMatchesTheWorkClone()
    {
        var master = PackagedMaster.Read("design-partner-master");

        master.Should().NotContain("what you run, you run in an upload's own container",
            "the work clone runs commands too (4778); a role that denies it stops the model trying things out");
        master.Should().Contain("what you try, you try in throwaway clones",
            "the role names both places the turn may run something");
        master.Should().NotContain("When `remember` is on your surface",
            "the remember tool describes itself; the skill does not restate it");
    }
}
