using AgentSmith.Application.Services.Activation;
using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Triage;

public sealed class TriageOutputProducerDeterministicTests
{
    [Fact]
    public async Task ProduceAsync_FilterNarrowsBeforeSelection_OnlyMatchingCandidatesAssigned()
    {
        var producer = NewProducer();
        var pipeline = NewPipelineWithSkills(
            Skill("matches", "producer", "source_available"),
            Skill("filtered_out", "producer", "NOT source_available"));
        SetSourceAvailable(pipeline, value: true);

        var output = await producer.ProduceAsync(pipeline, CancellationToken.None);

        output.Phases[PipelinePhase.Plan].Lead.Should().Be("matches");
    }

    [Fact]
    public async Task ProduceAsync_PhaseExceedsCap_TrimmedBySpecificity()
    {
        var producer = NewProducer(maxSkillsPerPhase: 2);
        var pipeline = NewPipelineWithSkills(
            Skill("high", "investigator", "source_available AND context_yaml_present"),
            Skill("mid", "investigator", "source_available"),
            Skill("low", "investigator", null));
        SetSourceAvailable(pipeline, value: true);
        SetContextYamlPresent(pipeline, value: true);

        var output = await producer.ProduceAsync(pipeline, CancellationToken.None);

        output.Phases[PipelinePhase.Plan].Analysts.Should().BeEquivalentTo(["high", "mid"]);
    }

    [Fact]
    public async Task ProduceAsync_TwoRuns_SameOutput()
    {
        var producer = NewProducer();
        var pipeline = NewPipelineWithSkills(
            Skill("planner", "producer"),
            Skill("inv-z", "investigator"),
            Skill("inv-a", "investigator"));

        var first = await producer.ProduceAsync(pipeline, CancellationToken.None);
        var second = await producer.ProduceAsync(pipeline, CancellationToken.None);

        first.Should().BeEquivalentTo(second);
    }

    private static TriageOutputProducer NewProducer(int maxSkillsPerPhase = 10)
    {
        var parser = new ActivationExpressionParser(new ActivationExpressionTokenizer());
        var scorer = new ActivationSpecificityScorer(parser,
            NullLogger<ActivationSpecificityScorer>.Instance);
        return new TriageOutputProducer(
            new DeterministicTriageSelector(scorer),
            new TriageLabelOverrideApplier(),
            new ActivationSkillFilter(parser, new ActivationEvaluator(),
                NullLogger<ActivationSkillFilter>.Instance),
            new PhaseSpecificityTrimmer(scorer, NullLogger<PhaseSpecificityTrimmer>.Instance),
            RunStateConceptsTestFactory.Default,
            new LoopLimitsConfig { MaxSkillsPerPhase = maxSkillsPerPhase },
            NullLogger<TriageOutputProducer>.Instance);
    }

    private static RoleSkillDefinition Skill(string name, string role, string? activatesWhen = null)
        => new()
        {
            Name = name,
            Description = name,
            Role = role,
            ActivatesWhen = activatesWhen
        };

    private static PipelineContext NewPipelineWithSkills(params RoleSkillDefinition[] skills)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.AvailableRoles, (IReadOnlyList<RoleSkillDefinition>)skills);
        pipeline.Set(ContextKeys.AgentConfig, new AgentConfig());
        return pipeline;
    }

    private static void SetSourceAvailable(PipelineContext pipeline, bool value) =>
        RunStateConceptsTestFactory.Default(pipeline).SetBool("source_available", value);

    private static void SetContextYamlPresent(PipelineContext pipeline, bool value) =>
        RunStateConceptsTestFactory.Default(pipeline).SetBool("context_yaml_present", value);
}
