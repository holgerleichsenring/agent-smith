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

        prompt.Should().Contain(SpecAttackPrompt.Brief + "\n\n" + SpecAttackPrompt.DecidabilityLens);
        SpecAttackPrompt.DecidabilityLens.Should().Contain("can the agent make it true by working")
            .And.Contain("can it see that it is true");
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
