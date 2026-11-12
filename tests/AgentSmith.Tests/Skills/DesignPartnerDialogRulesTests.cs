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
/// <para>
/// 2026-11-12-681f: v5.29.0 folded the references back into the master and dropped "Converging"; a proposal comes
/// as soon as one can be defended.
/// </para>
/// </summary>
public sealed class DesignPartnerDialogRulesTests
{
    /// <summary>First agent-smith-skills release carrying all three rules (master 1.6.3).</summary>
    private static readonly Version DialogRulesRelease = new(5, 5, 0);

    private static string Master() => PackagedMaster.Folded("design-partner-master");

    /// <summary>
    /// The conversation may be in any language; the artifacts it produces may not. 2026-11-12-681f: one sentence
    /// covers specs, tickets and bugs, the three items a design turn writes for a run to read.
    /// </summary>
    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_DraftsInEnglish() =>
        Master().Should().Contain(
            "Specs, tickets and bugs are English whatever language the chat is in.",
            $"the pin is {PackagedMaster.Pin} and this rule ships from v{DialogRulesRelease}; "
            + "a German spec draft is read later by a derivation, an executing agent and "
            + "reviewers who did not sit in the chat");

    /// <summary>
    /// A child of an epic is worked by its own coding pass, so the done list is the part that says when it is
    /// finished. A parent's done list does not reach it.
    /// </summary>
    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_AsksEachEpicChildForDone() =>
        Master().Should().Contain(
            "each child carries its own done lines, facts and assumptions",
            $"the pin is {PackagedMaster.Pin} and this rule ships from v{DialogRulesRelease}; "
            + "a child without one has no finish line");

    /// <summary>
    /// 2026-11-09-0620j: a request for work is answered with what was found and, as soon as one can be defended, a
    /// proposal — it is shown for approval and filed only on approval. The skill says so, and no longer keys
    /// proposing to a phrase.
    /// </summary>
    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_DraftsAsSoonAsItCanDefendOne()
    {
        var master = Master();

        master.Should().Contain(
            "A request for work is answered with what you found and, as soon as you can defend one, a proposal",
            $"the pin is {PackagedMaster.Pin}; a first turn that can defend a cut proposes it");
        master.Should().Contain("the operator approves what gets filed",
            "the master is told why an early draft is safe: it is approved before anything is filed");
        master.Should().NotContain("Ready to propose", "nothing waits for a phrase any more");
        master.Should().NotContain("always an **answer**", "the first-turn ban is gone on both sides");
    }
}
