using AgentSmith.Application.Services.Activation;
using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Triage;

public sealed class DeterministicTriageSelectorTests
{
    private static DeterministicTriageSelector Build()
    {
        var parser = new ActivationExpressionParser(new ActivationExpressionTokenizer());
        var scorer = new ActivationSpecificityScorer(parser,
            NullLogger<ActivationSpecificityScorer>.Instance);
        return new DeterministicTriageSelector(scorer);
    }

    private static RoleSkillDefinition Skill(string name, string role, string? activatesWhen = null)
        => new()
        {
            Name = name,
            Description = name,
            Role = role,
            ActivatesWhen = activatesWhen
        };

    [Fact]
    public void Select_SingleProducer_AssignsLeadInPlanAndReview()
    {
        var output = Build().Select([Skill("planner-1", "producer")]);

        output.Phases[PipelinePhase.Plan].Lead.Should().Be("planner-1");
        output.Phases[PipelinePhase.Review].Lead.Should().Be("planner-1");
        output.Phases[PipelinePhase.Final].Lead.Should().BeNull();
    }

    [Fact]
    public void Select_MultipleProducers_HighestSpecificityWinsLead()
    {
        var output = Build().Select([
            Skill("low-spec", "producer", activatesWhen: "source_available"),
            Skill("high-spec", "producer", activatesWhen: "source_available AND context_yaml_present"),
        ]);

        output.Phases[PipelinePhase.Plan].Lead.Should().Be("high-spec");
    }

    [Fact]
    public void Select_TiedSpecificity_AlphabeticalByNameWinsLead()
    {
        var output = Build().Select([
            Skill("zeta", "producer", activatesWhen: "source_available"),
            Skill("alpha", "producer", activatesWhen: "source_available"),
        ]);

        output.Phases[PipelinePhase.Plan].Lead.Should().Be("alpha");
    }

    [Fact]
    public void Select_AllInvestigators_AssignedAsAnalystsInPlan()
    {
        var output = Build().Select([
            Skill("p", "producer"),
            Skill("inv-a", "investigator"),
            Skill("inv-b", "investigator"),
        ]);

        output.Phases[PipelinePhase.Plan].Analysts.Should().BeEquivalentTo(new[] { "inv-a", "inv-b" });
        output.Phases[PipelinePhase.Review].Analysts.Should().BeEmpty();
    }

    [Fact]
    public void Select_AllJudges_AssignedAsReviewersInReview()
    {
        var output = Build().Select([
            Skill("p", "producer"),
            Skill("judge-x", "judge"),
            Skill("judge-y", "judge"),
        ]);

        output.Phases[PipelinePhase.Review].Reviewers.Should().BeEquivalentTo(new[] { "judge-x", "judge-y" });
        output.Phases[PipelinePhase.Plan].Reviewers.Should().BeEmpty();
    }

    [Fact]
    public void Select_FilterPresent_AssignedInFinalOnly()
    {
        var output = Build().Select([
            Skill("p", "producer"),
            Skill("f", "filter"),
        ]);

        output.Phases[PipelinePhase.Final].Filter.Should().Be("f");
        output.Phases[PipelinePhase.Plan].Filter.Should().BeNull();
        output.Phases[PipelinePhase.Review].Filter.Should().BeNull();
    }

    [Fact]
    public void Select_NoCandidates_AllPhasesEmpty()
    {
        var output = Build().Select(Array.Empty<RoleSkillDefinition>());

        output.Phases[PipelinePhase.Plan].Lead.Should().BeNull();
        output.Phases[PipelinePhase.Plan].Analysts.Should().BeEmpty();
        output.Phases[PipelinePhase.Review].Reviewers.Should().BeEmpty();
        output.Phases[PipelinePhase.Final].Filter.Should().BeNull();
        output.Rationale.Should().Contain("no candidates");
    }

    [Fact]
    public void Select_TwoIdenticalRuns_SameOutput()
    {
        var skills = new[]
        {
            Skill("c", "investigator", activatesWhen: "source_available"),
            Skill("a", "investigator", activatesWhen: "source_available AND context_yaml_present"),
            Skill("b", "investigator", activatesWhen: "source_available"),
        };

        var first = Build().Select(skills);
        var second = Build().Select(skills);

        first.Phases[PipelinePhase.Plan].Analysts
            .Should().BeEquivalentTo(second.Phases[PipelinePhase.Plan].Analysts,
                options => options.WithStrictOrdering());
        first.Phases[PipelinePhase.Plan].Analysts.First().Should().Be("a",
            "highest specificity wins; ties go alphabetical");
    }

    [Fact]
    public void Select_ConfidenceAlways100()
    {
        var output = Build().Select([Skill("p", "producer")]);

        output.Confidence.Should().Be(100,
            "deterministic selection has no judgment uncertainty");
    }

    [Fact]
    public void Select_RationaleMentionsCandidatesByRole()
    {
        var output = Build().Select([
            Skill("p", "producer"),
            Skill("i", "investigator"),
        ]);

        output.Rationale.Should().Contain("producer=1").And.Contain("investigator=1");
    }
}
