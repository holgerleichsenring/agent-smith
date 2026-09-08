using FluentAssertions;

namespace AgentSmith.Tests.Prompts;

/// <summary>
/// p0400a: run b9b0 proved that documenting ships_code was not enough — the
/// derivation omitted it on a pure-knowledge phase and the keystone failed a
/// 7/7-done phase for lacking a diff it never promised. The prompt now states
/// the obligation; this pins the load-bearing wording.
/// <para>
/// p0442: the prompt is served by the PINNED SKILLS CATALOG now, not by an embedded
/// resource — so these assertions are what makes bumping the pin a decision rather than
/// a version string. A release that drops one of these rules fails the build here.
/// </para>
/// </summary>
public sealed class SpecDerivationPromptTests
{
    /// <summary>
    /// p0421: the obligation moved from declaring a flag to WRITING CHECKABLE CRITERIA.
    /// After the phase runs, a reader who did not do the work is handed the criteria and
    /// the branch diff and has to tie each one to a file — so a criterion nobody can tie
    /// to anything fails the phase that honoured it.
    /// </summary>
    [Fact]
    public void DerivationPrompt_DemandsCriteriaThatCanBeCheckedAgainstTheRepository()
    {
        var prompt = DerivationPrompt();

        prompt.Should().Contain("CHECKED AGAINST THE REPOSITORY");
        prompt.Should().Contain("the branch diff");
        prompt.Should().NotContain("ships_code",
            "the declaration existed to except the old gate from its own question");
    }

    /// <summary>
    /// p0413: run 1b4b cut a mechanical ticket into three phases, each with a full
    /// master loop, and burned $10 without finishing the first. The prompt must
    /// state the cut-sizing rule the classified shape feeds — as a RULE about the
    /// work, with no example that names an ecosystem, a tool or a language.
    /// </summary>
    [Fact]
    public void DerivationPrompt_SizesTheCutToTheShapeOfTheWork()
    {
        var prompt = DerivationPrompt();

        prompt.Should().Contain("THE CUT IS SIZED TO THE SHAPE OF THE WORK");
        prompt.Should().Contain("FEWEST phases its deliverable allows",
            "deterministic work must be told to collapse, not merely allowed to");
        prompt.Should().Contain("a step per target turns one operation into one round of work",
            "the measured failure was one model round trip per target, not the phase count alone");
        prompt.Should().Contain("No shape stated means cut as you otherwise would",
            "an unclassified ticket must reach the cut it always got");
    }

    /// <summary>
    /// 2026-09-07-b7e2: the derivation wrote criteria on a guess about the code, and the
    /// guess became binding. The master is offered read-only tools under a look budget and
    /// must use them BEFORE a criterion rests on repository content; every fact cites the
    /// evidence id of the tool result it came from, and one that does not is recorded as an
    /// assumption by the reader. Ships with the v5.1.0 pin (skills PR #179).
    /// </summary>
    [Fact]
    public void SpecDerivationMaster_MayLookBeforeItWrites()
    {
        var prompt = DerivationPrompt();

        prompt.Should().Contain("LOOK BEFORE YOU WRITE");
        prompt.Should().Contain("each citing the id of the result it came from",
            "a fact is only a fact when a tool result stands behind it");
        prompt.Should().Contain("recorded as an ASSUMPTION",
            "an uncited claim must be downgraded by the reader, not silently believed");
    }

    /// <summary>
    /// 2026-09-07-c9d4: a ticket that reads two ways, where the code cannot settle which
    /// and the work differs, is a question for the author — a hand-back that carries both
    /// readings and the one the master would take, so an unanswered question can proceed on
    /// that reading next run. Ships with the v5.1.0 pin (skills PR #180).
    /// </summary>
    [Fact]
    public void SpecDerivationMaster_AsksInsteadOfChoosingSilently()
    {
        var prompt = DerivationPrompt();

        prompt.Should().Contain("\"question\"");
        prompt.Should().Contain("the code cannot settle which");
        prompt.Should().Contain("put the index of the one you would take in \"taken\"",
            "the next run proceeds on the taken reading when nobody answers");
        prompt.Should().Contain("An assumption that does not change the work is NOT a hand-back",
            "a question that changes nothing is a silent choice the master should make itself");
    }

    /// <summary>
    /// 2026-09-08-1830: a dependency ticket naming two contexts was cut for one, every
    /// criterion was met, the run went green, and the other context was never touched. The
    /// master now states per phase which of the named contexts it changes ("contexts") and
    /// accounts for every one it does not carry ("discarded_contexts" with a reason), so the
    /// system can compare the two lists and refuse a cut that leaves a context unspoken for.
    /// The "question" hand-back gains its worked example. Ships with the v5.2.0 pin (skills
    /// PR #182).
    /// </summary>
    [Fact]
    public void SpecDerivationMaster_NamesTheContextsEachPhaseCarries()
    {
        var prompt = DerivationPrompt();

        prompt.Should().Contain("\"contexts\"");
        prompt.Should().Contain("\"discarded_contexts\"");
        prompt.Should().Contain("EVERY NAMED CONTEXT IS SPOKEN FOR");
        prompt.Should().Contain(
            "either carried by at least one phase or listed in \"discarded_contexts\" with the reason it is outside the work",
            "a context the ticket names must land in one list or the other, never in neither");
        prompt.Should().Contain("refuses a cut that leaves a named context unaccounted for",
            "the master must know the comparison is enforced, not advisory");
        prompt.Should().Contain("adopt the newest versions, even for breaking changes",
            "the question case carries a worked example of a reading the code cannot settle");
        prompt.Should().Contain("That is a question, not a silent choice");
    }

    // The prompt as the pinned catalog ships it, with WHITESPACE COLLAPSED: these
    // assertions are about the master's wording, and an authored markdown file wraps its
    // lines where the author felt like it. "the branch diff" straddles a line break in
    // v4.5.0 — a rule that is present must not read as missing because of a newline.
    private static string DerivationPrompt() =>
        System.Text.RegularExpressions.Regex.Replace(
            PackagedMaster.Read("spec-derivation-master"), @"\s+", " ");
}
