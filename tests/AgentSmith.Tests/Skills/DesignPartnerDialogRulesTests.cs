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
/// <para>
/// 2026-11-05-862ec: the English and epic-child rules moved with their formats into the
/// design-outcome references; the discussion gate became "Converging", ending at a
/// "Ready to propose" line rather than at the operator's first reply.
/// </para>
/// </summary>
public sealed class DesignPartnerDialogRulesTests
{
    /// <summary>First agent-smith-skills release carrying all three rules (master 1.6.3).</summary>
    private static readonly Version DialogRulesRelease = new(5, 5, 0);

    private static string Master() => PackagedMaster.Read("design-partner-master");

    /// <summary>First release whose convergence ends at a "Ready to propose" line (master 2.0.0).</summary>
    private static readonly Version ConvergenceRelease = new(5, 25, 0);

    /// <summary>
    /// The conversation may be in any language; the artifacts it produces may not.
    /// Both drafted artifacts are covered — the spec block and the fix-bug ticket.
    /// </summary>
    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_DraftsInEnglish()
    {
        var master = PackagedMaster.Reference("design-outcome-spec");

        master.Should().Contain(
            "ENGLISH ONLY, whatever language the conversation is in.",
            $"the pin is {PackagedMaster.Pin} and this rule ships from v{DialogRulesRelease}; "
            + "a German spec draft is read later by a derivation, an executing agent and "
            + "reviewers who did not sit in the chat");

        master.Should().Contain(
            "tests and done are English even when every turn above them is not.",
            "the rule has to reach the fields an operator dictates in their own language, "
            + "not only the prose around them");

        PackagedMaster.Reference("design-outcome-bug").Should().Contain(
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
        var master = PackagedMaster.Reference("design-outcome-epic");

        master.Should().Contain(
            "Every child carries `done`",
            $"the pin is {PackagedMaster.Pin} and this rule ships from v{DialogRulesRelease}; "
            + "a child filed without one is a ticket with no finish line");

        master.Should().Contain(
            "done list is the part of the ticket that says when the",
            "the rule has to say WHY a child needs its own list, or a master that sees the "
            + "parent's list reads the requirement as already met");

        master.Should().Contain(
            "conversation has not settled when a slice is done, the child carries",
            "2026-11-11-205fb: an unsettled done list is a proposal marked on the card, not a "
            + "reason to hold the draft back");
    }

    /// <summary>
    /// 2026-11-09-0620j: a request for work is answered with what was found and, as soon as one can be defended, a
    /// draft — the server no longer refuses a first-turn proposal, because a draft is shown under review and filed
    /// only on approval. The skill says so, and no longer keys proposing to a phrase.
    /// </summary>
    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_DraftsAsSoonAsItCanDefendOne()
    {
        var master = Master();

        master.Should().Contain(
            "## Converging",
            $"the pin is {PackagedMaster.Pin} and this section ships from v{ConvergenceRelease}");
        master.Should().Contain(
            "A request for work is answered with what you found and, as soon as you",
            "a first turn that can defend a cut proposes it");
        master.Should().Contain(
            "A draft is not a filing. The framework validates it, shows it to the",
            "the master is told why an early draft is safe: it is reviewed and approved before anything is filed");
        master.Should().NotContain("Ready to propose", "nothing waits for a phrase any more");
        master.Should().NotContain("always an **answer**", "the first-turn ban is gone on both sides");
    }
}
