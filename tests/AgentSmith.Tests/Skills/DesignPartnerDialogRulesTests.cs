using AgentSmith.Tests.Prompts;
using FluentAssertions;

namespace AgentSmith.Tests.Skills;

/// <summary>
/// 2026-09-17-6a29: three rules the spec dialog leans on, read from the PINNED master.
/// <para>
/// The text is authored in agent-smith-skills; this repo carries it only through
/// <c>SkillsCatalogVersion</c>. Each rule fixes a failure the framework cannot fix on
/// its own side: a German draft splits the spec record in two, an epic child filed as a
/// ticket weeks later has nothing saying when it is finished, and a proposal that
/// arrives before the operator has replied is a spec written from a half-formed idea.
/// A release that drops one of them fails here rather than on a live run.
/// </para>
/// </summary>
public sealed class DesignPartnerDialogRulesTests
{
    /// <summary>First agent-smith-skills release carrying all three rules (master 1.6.3).</summary>
    private static readonly Version DialogRulesRelease = new(5, 5, 0);

    private static string Master() => PackagedMaster.Read("design-partner-master");

    /// <summary>
    /// The conversation may be in any language; the artifacts it produces may not.
    /// Both drafted artifacts are covered — the phase spec block and the fix-bug ticket.
    /// </summary>
    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_DraftsInEnglish()
    {
        var master = Master();

        master.Should().Contain(
            "ENGLISH ONLY, whatever language the conversation is in.",
            $"the pin is {PackagedMaster.Pin} and this rule ships from v{DialogRulesRelease}; "
            + "a German phase draft is read later by a derivation, an executing agent and "
            + "reviewers who did not sit in the chat");

        master.Should().Contain(
            "tests and done are English even when every turn above them is not.",
            "the rule has to reach the fields an operator dictates in their own language, "
            + "not only the prose around them");

        master.Should().Contain(
            "both are ENGLISH whatever",
            "a fix-bug ticket outlives the chat and is executed as-is by the pipeline, so "
            + "the English rule covers the ticket draft too");
    }

    /// <summary>
    /// A child of an epic is filed as its own ticket and worked much later, so the done
    /// list is the part that says when it is finished. A parent's done list does not
    /// reach it.
    /// </summary>
    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_AsksEachEpicChildForDone()
    {
        var master = Master();

        master.Should().Contain(
            "Every child carries `done`",
            $"the pin is {PackagedMaster.Pin} and this rule ships from v{DialogRulesRelease}; "
            + "a child filed without one is a ticket with no finish line");

        master.Should().Contain(
            "done list is the part of the ticket that says when it is finished.",
            "the rule has to say WHY a child needs its own list, or a master that sees the "
            + "parent's list reads the requirement as already met");

        master.Should().Contain(
            "conversation has not settled when a slice is done, that is still open:",
            "without this the master invents a done list to satisfy the rule — the missing "
            + "answer is a question for the operator, not a placeholder");
    }

    /// <summary>
    /// The first reply to a request for work is an answer, never a proposal. The
    /// framework refuses an early proposal, so a master that does not know the rule
    /// spends a turn on an outcome that is never shown.
    /// </summary>
    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_DiscussesBeforeItProposes()
    {
        var master = Master();

        master.Should().Contain(
            "### Discussion comes first",
            $"the pin is {PackagedMaster.Pin} and this section ships from v{DialogRulesRelease}");

        master.Should().Contain(
            "The first reply to a request for work is always an **answer**",
            "however clear a request looks, a spec drafted before the grounding is shown "
            + "is a spec the operator never got to steer");

        master.Should().Contain(
            "The work has **converged** when the operator has replied",
            "the master needs the test for when discussion ENDS, or 'discuss first' "
            + "becomes an extra turn rather than a gate");

        master.Should().Contain(
            "refuses one that comes before the operator has",
            "the master is told the framework enforces this, so an early proposal reads "
            + "as a wasted turn rather than a style preference");
    }
}
