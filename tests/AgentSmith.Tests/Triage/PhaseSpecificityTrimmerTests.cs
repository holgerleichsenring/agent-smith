using AgentSmith.Application.Services.Activation;
using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Triage;

public sealed class PhaseSpecificityTrimmerTests
{
    private readonly PhaseSpecificityTrimmer _sut = new(
        new ActivationSpecificityScorer(
            new ActivationExpressionParser(new ActivationExpressionTokenizer()),
            NullLogger<ActivationSpecificityScorer>.Instance),
        NullLogger<PhaseSpecificityTrimmer>.Instance);

    [Fact]
    public void Trim_PhaseExceedsCap_TrimsLowestSpecificity()
    {
        var skills = new List<RoleSkillDefinition>
        {
            new() { Name = "high",   ActivatesWhen = "source_available AND context_yaml_present" },
            new() { Name = "medium", ActivatesWhen = "source_available" },
            new() { Name = "low",    ActivatesWhen = null },
        };
        var output = OutputWithAnalysts("high", "medium", "low");

        var trimmed = _sut.Trim(output, skills, cap: 2);

        trimmed.Phases[PipelinePhase.Plan].Analysts.Should().BeEquivalentTo(["high", "medium"]);
    }

    [Fact]
    public void Trim_PhaseExactlyAtCap_NotTrimmed()
    {
        var skills = new List<RoleSkillDefinition>
        {
            new() { Name = "a", ActivatesWhen = "source_available" },
            new() { Name = "b", ActivatesWhen = "source_available" },
        };
        var output = OutputWithAnalysts("a", "b");

        _sut.Trim(output, skills, cap: 2).Phases[PipelinePhase.Plan].Analysts
            .Should().BeEquivalentTo(["a", "b"]);
    }

    [Fact]
    public void Trim_PhaseUnderCap_NotTrimmed()
    {
        var skills = new List<RoleSkillDefinition>
        {
            new() { Name = "a", ActivatesWhen = "source_available" },
        };
        var output = OutputWithAnalysts("a");

        _sut.Trim(output, skills, cap: 5).Phases[PipelinePhase.Plan].Analysts
            .Should().BeEquivalentTo(["a"]);
    }

    [Fact]
    public void Trim_TieBreakByName_DeterministicOrder()
    {
        var skills = new List<RoleSkillDefinition>
        {
            new() { Name = "zebra", ActivatesWhen = "source_available" },
            new() { Name = "alpha", ActivatesWhen = "source_available" },
            new() { Name = "mango", ActivatesWhen = "source_available" },
        };
        var output = OutputWithAnalysts("zebra", "alpha", "mango");

        var trimmed = _sut.Trim(output, skills, cap: 2);

        trimmed.Phases[PipelinePhase.Plan].Analysts.Should().BeEquivalentTo(["alpha", "mango"]);
    }

    [Fact]
    public void Trim_LeadAndFilterCounted_AnalystsCutFirst()
    {
        var skills = new List<RoleSkillDefinition>
        {
            new() { Name = "leader",   ActivatesWhen = "source_available" },
            new() { Name = "filter",   ActivatesWhen = "source_available" },
            new() { Name = "highScore", ActivatesWhen = "source_available AND context_yaml_present" },
            new() { Name = "lowScore",  ActivatesWhen = null },
        };
        var output = new TriageOutput(
            new Dictionary<PipelinePhase, PhaseAssignment>
            {
                [PipelinePhase.Plan] = new(
                    Lead: "leader",
                    Analysts: ["highScore", "lowScore"],
                    Reviewers: [],
                    Filter: "filter"),
            },
            Confidence: 90,
            Rationale: "test");

        var trimmed = _sut.Trim(output, skills, cap: 3);

        var assignment = trimmed.Phases[PipelinePhase.Plan];
        assignment.Lead.Should().Be("leader");
        assignment.Filter.Should().Be("filter");
        assignment.Analysts.Should().BeEquivalentTo(["highScore"]);
    }

    [Fact]
    public void Trim_OtherPhasesUntouched_OnlyOverflowingPhaseTrimmed()
    {
        var skills = new List<RoleSkillDefinition>
        {
            new() { Name = "a", ActivatesWhen = null },
            new() { Name = "b", ActivatesWhen = null },
            new() { Name = "c", ActivatesWhen = null },
        };
        var output = new TriageOutput(
            new Dictionary<PipelinePhase, PhaseAssignment>
            {
                [PipelinePhase.Plan]   = new(null, ["a"],          [], null),
                [PipelinePhase.Review] = new(null, ["a", "b", "c"], [], null),
            },
            Confidence: 80,
            Rationale: "");

        var trimmed = _sut.Trim(output, skills, cap: 2);

        trimmed.Phases[PipelinePhase.Plan].Analysts.Should().BeEquivalentTo(["a"]);
        trimmed.Phases[PipelinePhase.Review].Analysts.Should().HaveCount(2);
    }

    [Fact]
    public void Trim_ReviewersOverflow_TrimsReviewersByScore()
    {
        var skills = new List<RoleSkillDefinition>
        {
            new() { Name = "highReviewer",  ActivatesWhen = "a AND b" },
            new() { Name = "lowReviewer",   ActivatesWhen = null },
        };
        var output = new TriageOutput(
            new Dictionary<PipelinePhase, PhaseAssignment>
            {
                [PipelinePhase.Plan] = new(
                    null, [], ["highReviewer", "lowReviewer"], null),
            },
            70, "");

        var trimmed = _sut.Trim(output, skills, cap: 1);

        trimmed.Phases[PipelinePhase.Plan].Reviewers.Should().BeEquivalentTo(["highReviewer"]);
    }

    private static TriageOutput OutputWithAnalysts(params string[] names) => new(
        new Dictionary<PipelinePhase, PhaseAssignment>
        {
            [PipelinePhase.Plan] = new(null, names, [], null),
        },
        Confidence: 80,
        Rationale: "");
}
