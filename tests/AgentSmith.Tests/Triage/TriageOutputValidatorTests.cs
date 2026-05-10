using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Models.Skills;
using FluentAssertions;

namespace AgentSmith.Tests.Triage;

/// <summary>
/// p0131a: validator shape simplified — legacy ActivationCriteria-bag retired,
/// rationale keys checked vocabulary-only, role-slot checks via
/// <see cref="SkillRoleMapping"/> against the skill's single new-format role.
/// </summary>
public sealed class TriageOutputValidatorTests
{
    private static readonly TriageRationaleParser RationaleParser = new();
    private readonly TriageOutputValidator _sut = new(RationaleParser);

    [Fact]
    public void Validate_RoleSlotMismatchesSkillRole_RejectsOutput()
    {
        // tester is an investigator → maps to Analyst, NOT Lead.
        var skills = new[] { SkillIndex("tester", role: "investigator") };
        var output = TriageWith(PipelinePhase.Plan, lead: "tester");

        var result = _sut.Validate(output, skills);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("tester") && e.Contains("Lead"));
    }

    [Fact]
    public void Validate_ProducerAssignedToLead_Accepts()
    {
        var skills = new[] { SkillIndex("architect-planner", role: "producer") };
        var vocabulary = Vocab(("auth-port", "auth-related boundary"));
        var output = new TriageOutput(
            new Dictionary<PipelinePhase, PhaseAssignment>
            {
                [PipelinePhase.Plan] = new("architect-planner", Array.Empty<string>(), Array.Empty<string>(), null)
            },
            85,
            "lead=architect-planner:auth-port;");

        var result = _sut.Validate(output, skills, vocabulary);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_RationaleKeyNotInVocab_Rejects()
    {
        var skills = new[] { SkillIndex("architect-planner", role: "producer") };
        var output = new TriageOutput(
            new Dictionary<PipelinePhase, PhaseAssignment>
            {
                [PipelinePhase.Plan] = new("architect-planner", Array.Empty<string>(), Array.Empty<string>(), null)
            },
            85,
            "lead=architect-planner:totally_made_up_concept;");

        var result = _sut.Validate(output, skills, Vocab(("real_key", "exists")));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("totally_made_up_concept"));
    }

    [Fact]
    public void Validate_RationaleExceedsMaxLength_Rejects()
    {
        var skills = Array.Empty<SkillIndexEntry>();
        var huge = new string('x', 501);
        var output = new TriageOutput(new Dictionary<PipelinePhase, PhaseAssignment>(), 85, huge);

        var result = _sut.Validate(output, skills);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("500 chars"));
    }

    [Fact]
    public void Validate_RationaleContainsNewlines_Rejects()
    {
        var skills = Array.Empty<SkillIndexEntry>();
        var output = new TriageOutput(
            new Dictionary<PipelinePhase, PhaseAssignment>(), 85, "lead=a:b;\nanalyst=c:d;");

        var result = _sut.Validate(output, skills);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("newlines"));
    }

    [Fact]
    public void Validate_UnknownSkillCited_Rejects()
    {
        var skills = Array.Empty<SkillIndexEntry>();
        var output = new TriageOutput(
            new Dictionary<PipelinePhase, PhaseAssignment>(), 85,
            "lead=ghost-skill:any-key;");

        var result = _sut.Validate(output, skills);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("unknown skill") && e.Contains("ghost-skill"));
    }

    [Fact]
    public void Validate_NullVocabulary_FallsBackToEmpty_RejectsAnyKey()
    {
        var skills = new[] { SkillIndex("architect-planner", role: "producer") };
        var output = new TriageOutput(
            new Dictionary<PipelinePhase, PhaseAssignment>
            {
                [PipelinePhase.Plan] = new("architect-planner", Array.Empty<string>(), Array.Empty<string>(), null)
            },
            85,
            "lead=architect-planner:any-key;");

        var result = _sut.Validate(output, skills, vocabulary: null);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("any-key"));
    }

    private static ConceptVocabulary Vocab(params (string Key, string Desc)[] entries)
    {
        var dict = entries.ToDictionary(
            e => e.Key,
            e => new ProjectConcept(e.Key, e.Desc, ConceptType.Bool, null, null, []));
        return new ConceptVocabulary(dict);
    }

    private static SkillIndexEntry SkillIndex(string name, string role) =>
        new(name, $"Skill {name}", role, OutputSchema: null, ActivatesWhen: null);

    private static TriageOutput TriageWith(PipelinePhase phase, string lead) =>
        new(new Dictionary<PipelinePhase, PhaseAssignment>
        {
            [phase] = new(lead, Array.Empty<string>(), Array.Empty<string>(), null)
        }, 85, string.Empty);
}
