using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Events;
using FluentAssertions;
using static AgentSmith.Tests.Specs.Review.AttackReviewFixtures;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>2026-10-13-8762e: what one fresh attack reviewer is told.</summary>
public sealed class SpecAttackPromptTests
{
    private const string PluginBrief =
        "Attack this spec before anyone builds it. Open every cited file around the cited lines — a valid citation is not a true claim. Follow every field, setting and value the spec touches to its sinks: who reads it, who writes it, what else breaks. Extract archives and generated files before reasoning about them. Check every step against the code as it is, not as the spec says it is. Answer with exactly one verdict — BUILD, BUILD WITH CUTS, or REFUSE — and back each finding with file:line evidence. For BUILD WITH CUTS name each cut precisely. REFUSE is a successful result when the spec is wrong.";

    [Fact]
    public void SpecAttackPrompt_CarriesThePluginBriefVerbatim()
    {
        SpecAttackPrompt.Brief.Should().Be(PluginBrief, "skills/review-spec/SKILL.md line 31 of the spec-first plugin");
        SpecAttackPrompt.For(Input(), Look()).Should().Contain(PluginBrief).And.Contain(Yaml.TrimEnd());
    }

    /// <summary>2026-11-11-205fd: every reviewer is told, right after the brief, that it reads and cannot run.</summary>
    [Fact]
    public void SpecAttackPrompt_Brief_IsFollowedByTheReadOnlyLine()
    {
        SpecAttackPrompt.For(Input(), Look()).Should().Contain(PluginBrief + " " + SpecAttackPrompt.ReadOnlyLine);
        SpecAttackPrompt.For(Input() with { Proposal = true }, Look()).Should().Contain(SpecAttackPrompt.ReadOnlyLine);
    }

    /// <summary>2026-11-10-14a3g: a design turn's reviewer judges the cut; a run's reviewer is asked as before.</summary>
    [Fact]
    public void SpecAttackPrompt_ProposalScope_AsksTheThreeJudgementQuestions()
    {
        var proposal = SpecAttackPrompt.For(Input() with { Proposal = true }, Look());
        var run = SpecAttackPrompt.For(Input(), Look());

        proposal.Should().Contain("can ONE coding run build this spec")
            .And.Contain("Can each done line be answered yes or no")
            .And.Contain("every capability the uploaded material lists as approved");
        run.Should().NotContain(SpecAttackJudgement.Lone).And.NotContain(SpecAttackJudgement.Epic);
    }

    /// <summary>2026-11-11-2bdfa: an epic child is asked for a capability no spec of the epic takes.</summary>
    [Fact]
    public void SpecAttackPrompt_ProposalJudgement_AsksForCapabilityNoSpecOfTheEpicTakes()
    {
        var input = Input() with
        {
            Proposal = true,
            Siblings = [new SpecAttackSibling("2026-11-11-0001b", "The import reads the file.", ["shop"], "The importer.", null)],
        };

        var prompt = SpecAttackPrompt.For(input, Look());

        prompt.Should().Contain(SpecAttackJudgement.Epic)
            .And.Contain("together with the other specs of this epic listed below")
            .And.Contain("name any that no spec of this epic takes")
            .And.NotContain("dropped or postponed?");
    }

    [Fact]
    public void SpecAttackPrompt_LoneSpec_KeepsTodaysJudgement()
    {
        var prompt = SpecAttackPrompt.For(Input() with { Proposal = true }, Look());

        prompt.Should().Contain(SpecAttackJudgement.Lone).And.Contain("name any that is dropped or postponed?")
            .And.NotContain(SpecAttackSiblingsSection.Heading);
    }

    [Fact]
    public void SpecAttackSiblingsSection_Render_ListsEachSiblingAndSitsBeforeTheSpec()
    {
        var siblings = new[]
        {
            new SpecAttackSibling("2026-11-11-0001b", "The import reads the file.", ["shop", "erp"], "The importer.", "The UI."),
        };

        var prompt = SpecAttackPrompt.For(Input() with { Proposal = true, Siblings = siblings }, Look());

        SpecAttackSiblingsSection.Render([]).Should().BeEmpty();
        prompt.Should().Contain("- 2026-11-11-0001b: The import reads the file.").And.Contain("repos: shop, erp")
            .And.Contain("scope in: The importer.").And.Contain("scope out: The UI.");
        prompt.IndexOf(SpecAttackSiblingsSection.Heading, StringComparison.Ordinal).Should()
            .BeLessThan(prompt.IndexOf("## The spec", StringComparison.Ordinal))
            .And.BeGreaterThan(prompt.IndexOf("## What the evidence check found", StringComparison.Ordinal));
    }

    [Fact]
    public void SpecAttackJudgement_For_IsEmptyOutsideADesignTurn()
    {
        SpecAttackJudgement.For(Input()).Should().BeEmpty();
    }

    [Fact]
    public void SpecAttackPrompt_ShowsEvidenceProblemsAndPriorResidues()
    {
        var input = Input() with
        {
            EvidenceProblems = [new SpecEvidenceProblem("The handler reads the store.", "not a file: src/Gone.cs")],
            PriorResidues = [new SpecReviewResidue(SpecRegisterNames.Tests, "every order of the day")],
        };

        var prompt = SpecAttackPrompt.For(input, Look());

        prompt.Should().Contain("The handler reads the store.: not a file: src/Gone.cs");
        prompt.Should().Contain("tests: \"every order of the day\"");
    }

    [Fact]
    public void SpecAttackPrompt_SaysHowManyCitationsWereNotChecked()
    {
        var prompt = SpecAttackPrompt.For(Input() with { NotChecked = 3, CitationsTotal = 7 }, Look());

        prompt.Should().Contain("3 of 7 citations were not checked").And.Contain("minted [Ln] look lines included");
    }

    [Fact]
    public void SpecAttackPrompt_AsksForQuotesOfAtLeastFourWords()
    {
        var prompt = SpecAttackPrompt.For(Input(), Look());

        prompt.Should().Contain("Every quote is a run of at least 4 consecutive words");
        foreach (var register in SpecRegisterNames.All) prompt.Should().Contain(register);
    }

    [Fact]
    public void AttackBrief_CarriesTheDecidabilityLens()
    {
        var prompt = SpecAttackPrompt.For(AttackReviewFixtures.Input(), null);

        prompt.Should().Contain(SpecAttackPrompt.Brief + " " + SpecAttackPrompt.ReadOnlyLine + "\n\n" + SpecAttackPrompt.DecidabilityLens);
        SpecAttackPrompt.DecidabilityLens.Should().Contain("can the agent make it true by working")
            .And.Contain("can it see that it is true");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Prompt_SaysAGapToDoneIsTheWork(bool describesToday)
    {
        var prompt = SpecAttackPrompt.For(Input() with { DescribesToday = describesToday }, Look());

        prompt.Should().Contain(DoneTargetState.Rule)
            .And.NotContain(SpecAttackPrompt.Brief + DoneTargetState.Rule, "the plugin's brief stays verbatim");
        prompt.IndexOf(DoneTargetState.Rule, StringComparison.Ordinal).Should()
            .BeGreaterThan(prompt.IndexOf("## How to answer", StringComparison.Ordinal), "it is said beside Today");
    }

    [Fact]
    public void AttackBrief_AbsenceCriterion_IsCutToOutsideTheMethodologyDirectory()
    {
        var prompt = SpecAttackPrompt.For(AttackReviewFixtures.Input(), null);

        prompt.Should().Contain("no file outside .agentsmith/ contains X")
            .And.Contain("because the spec committed under .agentsmith/ quotes X")
            .And.Contain("your read of a file that holds X today");
    }
}
