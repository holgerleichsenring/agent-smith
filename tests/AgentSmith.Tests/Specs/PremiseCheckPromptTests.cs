using AgentSmith.Application.Services.Specs;
using FluentAssertions;
using static AgentSmith.Tests.Specs.PremiseCheckTestDoubles;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-17-0e79c: the prompt describes the check's OWN look, or the tools carry repository
/// names the reader was never shown and every call is refused by name resolution before it
/// costs anything — ffa7's finding, which holds for any second holder of a look.
/// </summary>
public sealed class PremiseCheckPromptTests
{
    /// <summary>
    /// 2026-09-24-485e: a live check reported a premise FALSE — "the manifest and lockfile are at
    /// the repository root" — citing a content grep whose pattern contained the FILENAME. A match
    /// on that proves some file MENTIONS the name; and the list it came from ended by saying more
    /// matches followed, so it proved nothing about the root at all. The premise named its paths,
    /// and reading them would have settled it. The prompt now says which look proves what.
    /// </summary>
    [Fact]
    public async Task PremiseCheck_Prompt_SaysAGivenPathIsSettledByReadingIt()
    {
        var h = For(Draft(), "[]");

        await h.RunAsync();

        var prompt = Flowed(h.Provider.Prompts.Should().ContainSingle().Subject);
        prompt.Should().Contain("gives a PATH is settled by READING that path")
            .And.Contain("does not exist");
    }

    /// <summary>
    /// 2026-09-24-4f10: the rule above was written for a premise that GIVES a path, and read as
    /// covering a premise that merely names a file it certified a guess. A live run was stopped
    /// on "the Client repository contains package.json" after one read of the repository ROOT —
    /// where a client's manifest ordinarily does not sit. The two cases are now separate.
    /// </summary>
    [Fact]
    public async Task PremiseCheck_Prompt_SeparatesAGivenPathFromABareFilename()
    {
        var h = For(Draft(), "[]");

        await h.RunAsync();

        var prompt = Flowed(h.Provider.Prompts.Should().ContainSingle().Subject);
        prompt.Should().Contain("names a FILE WITHOUT SAYING WHERE IT SITS")
            .And.Contain("Reading a guess proves that one path empty and nothing else");
    }

    [Fact]
    public async Task PremiseCheck_Prompt_SaysAbsenceFromAWholeRepositoryCannotBeShown()
    {
        var h = For(Draft(), "[]");

        await h.RunAsync();

        // There is no name or glob look: search_repository matches CONTENT, read_file answers
        // about one path. Unproven stops nothing, which is the right cost for what cannot be seen.
        Flowed(h.Provider.Prompts.Should().ContainSingle().Subject)
            .Should().Contain("No look offered here can show a file absent from a whole repository");
    }

    [Fact]
    public async Task PremiseCheck_Prompt_SaysAContentMatchNeverProvesWhereAFileSits()
    {
        var h = For(Draft(), "[]");

        await h.RunAsync();

        Flowed(h.Provider.Prompts.Should().ContainSingle().Subject)
            .Should().Contain("never proves where a file sits")
            .And.Contain("MENTIONS that name");
    }

    [Fact]
    public async Task PremiseCheck_Prompt_SaysATruncatedListProvesNoAbsence()
    {
        var h = For(Draft(), "[]");

        await h.RunAsync();

        Flowed(h.Provider.Prompts.Should().ContainSingle().Subject)
            .Should().Contain("HEAD, not an inventory")
            .And.Contain("Nothing is absent because it is not in it");
    }

    [Fact]
    public async Task PremiseCheck_Prompt_NamesTheRepositoriesAllowanceAndIdSpelling()
    {
        var h = For(Draft(), "[]");

        await h.RunAsync();

        var prompt = h.Provider.Prompts.Should().ContainSingle().Subject;
        prompt.Should().Contain("## Repositories you may look into")
            .And.Contain($"- {DerivationTestLooks.Repo}")
            .And.Contain($"up to {DerivationLookTerms.CutReviewAllowance} looks")
            .And.Contain("such as [M3]");
        // Not "does not contain [L3]", which a fixture saying [L2] passes without testing
        // anything: NO bracketed id under any other letter may appear, because an id the reader
        // cannot cite is an invitation to cite it.
        System.Text.RegularExpressions.Regex
            .Matches(prompt, @"\[([A-Za-z]+)\d+\]")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .Should().BeEquivalentTo(["M"]);
    }

    [Fact]
    public async Task PremiseCheck_Prompt_CarriesNoEvidenceIdMintedByTheDerivation()
    {
        // A fact's stored evidence is the DERIVATION's look, under the derivation's letter.
        var h = For(
            Draft(fact: "the bus client is a singleton") with
            {
                Facts = [new Contracts.Models.PhaseFact(
                    "the bus client is a singleton",
                    "[L7] Sample.Server: the derivation ran 'grep -n Singleton' exited 0")],
            },
            "[]");

        await h.RunAsync();

        var prompt = h.Provider.Prompts.Single();
        prompt.Should().NotContain("[L7]", "the reader may cite only ids this check minted")
            .And.Contain("the derivation ran 'grep -n Singleton'",
                "the hint of where it was seen survives; only the foreign id is stripped");
    }

    [Fact]
    public async Task PremiseCheck_Prompt_NamesThePhasesOfThisSetThatAlreadyRan()
    {
        // A set is a sequence: slice 2's premises are written against the state slice 1 leaves,
        // and slice 1's commits are in the very sandbox this reader looks into.
        var h = For(Draft(), "[]");
        SetWithAnExecutedPredecessor(h.Pipeline, Draft());

        await h.RunAsync();

        var prompt = h.Provider.Prompts.Single();
        prompt.Should().Contain("## Phases of this specification that have already run")
            .And.Contain("2026-09-17-0000: split the sender out")
            .And.Contain("COMMITTED in the repositories you are looking at");
    }

    [Fact]
    public async Task PremiseCheck_Prompt_FirstPhaseOfASet_NamesNoPredecessor()
    {
        var h = For(Draft(), "[]");

        await h.RunAsync();

        h.Provider.Prompts.Single().Should().NotContain("already run");
    }

    [Fact]
    public async Task PremiseCheck_DecisionsTextReachesTheCheckerAsProse()
    {
        var h = For(Draft(), "[]");

        await h.RunAsync();

        var prompt = h.Provider.Prompts.Single();
        prompt.Should().Contain("DECISIONS")
            .And.Contain("The sender stays in Api.Orders because OrderHandler.cs:34-41 already owns dispatch.")
            .And.Contain("FACTS").And.Contain("OrderHandler validates the payload before dispatch")
            .And.Contain("ASSUMPTIONS").And.Contain("the bus client is registered as a singleton");
    }

    [Fact]
    public async Task PremiseCheck_ToolCallOnAnUnlistedRepository_IsRefusedByName()
    {
        var h = For(Draft(), "[]", searches: ("some-other-repo", "Validate"));

        await h.RunAsync();

        h.Provider.Seen.Should().Contain(r => r.Contains("No repository named 'some-other-repo'"));
        h.Provider.Seen.Should().Contain(r => r.Contains(DerivationTestLooks.Repo),
            "the refusal names what the run does carry, which the prompt listed");
    }

    [Fact]
    public void PremiseCheck_TermsAndTheirLetter_AreItsOwn()
    {
        var terms = DerivationLookTerms.PremiseCheck;

        terms.EvidencePrefix.Should().Be("M");
        terms.Actor.Should().Be("premise check");
        new[]
        {
            DerivationLookTerms.Derivation.EvidencePrefix,
            DerivationLookTerms.ProposalReview.EvidencePrefix,
            terms.EvidencePrefix,
        }.Should().OnlyHaveUniqueItems("two holders under one letter resolve to the wrong look");
    }

    [Fact]
    public void PremiseCheckLook_OverARunWithNoSandbox_IsNull()
    {
        DerivationTestLooks.Factory().ForPremiseCheck(new Contracts.Commands.PipelineContext())
            .Should().BeNull("every verdict this check reaches needs a look that ran");
    }

    /// <summary>A two-phase set whose FIRST phase is done, as the sequence leaves it.</summary>
    private static void SetWithAnExecutedPredecessor(
        Contracts.Commands.PipelineContext pipeline, Contracts.Models.PhaseDraft current)
    {
        var first = new Contracts.Models.PhaseDraft(
            "2026-09-17-0000", "split the sender out", "phase: 2026-09-17-0000", []);
        pipeline.Set(Contracts.Commands.ContextKeys.SpecSet, new Contracts.Specs.SpecSet(
            "azdo-1",
            [
                new Contracts.Specs.SpecPhase(first, first.PhaseId, string.Empty, []),
                new Contracts.Specs.SpecPhase(current, current.PhaseId, string.Empty, []),
            ],
            Contracts.Specs.SpecAccounting.Empty, [], Contracts.Specs.SpecSource.Derived));
        pipeline.Set(
            Contracts.Commands.ContextKeys.SpecSequenceProgress,
            new Contracts.Specs.SpecSequenceProgress(
            [
                new Contracts.Specs.PhaseProgress(
                    first.PhaseId, first.Goal, Contracts.Specs.PhaseRunState.Done),
                new Contracts.Specs.PhaseProgress(
                    current.PhaseId, current.Goal, Contracts.Specs.PhaseRunState.InProgress),
            ]));
    }

    /// <summary>The prompt is prose and wraps; a rule about its WORDING must not also be a rule
    /// about where the lines break.</summary>
    private static string Flowed(string prompt) =>
        System.Text.RegularExpressions.Regex.Replace(prompt, @"\s+", " ");
}
