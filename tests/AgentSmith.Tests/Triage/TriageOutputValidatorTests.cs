using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Models.Skills;
using FluentAssertions;

namespace AgentSmith.Tests.Triage;

public sealed class TriageOutputValidatorTests
{
    private static readonly TriageRationaleParser RationaleParser = new();
    private readonly TriageOutputValidator _sut = new(RationaleParser);

    [Fact]
    public void Validate_RoleNotInRolesSupported_RejectsOutput()
    {
        var skills = new[] { SkillIndex("tester", roles: new[] { SkillRole.Analyst, SkillRole.Reviewer }) };
        var output = TriageWith(PipelinePhase.Plan, lead: "tester");

        var result = _sut.Validate(output, skills);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("tester") && e.Contains("Lead"));
    }

    [Fact]
    public void Validate_RationaleKeyNotInSkill_RejectsOutput()
    {
        var skills = new[] { SkillIndex("architect", roles: new[] { SkillRole.Lead }, positiveKeys: new[] { "auth-port" }) };
        var output = new TriageOutput(
            new Dictionary<PipelinePhase, PhaseAssignment>
            {
                [PipelinePhase.Plan] = new("architect", Array.Empty<string>(), Array.Empty<string>(), null)
            },
            85,
            "lead=architect:invented-key;");

        var result = _sut.Validate(output, skills);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("invented-key"));
    }

    [Fact]
    public void Validate_RationaleExceedsMaxLength_RejectsOutput()
    {
        var skills = Array.Empty<SkillIndexEntry>();
        var huge = new string('x', 301);
        var output = new TriageOutput(
            new Dictionary<PipelinePhase, PhaseAssignment>(), 85, huge);

        var result = _sut.Validate(output, skills);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("300 chars"));
    }

    [Fact]
    public void Validate_OutputContainsNewlines_RejectsOutput()
    {
        var skills = Array.Empty<SkillIndexEntry>();
        var output = new TriageOutput(
            new Dictionary<PipelinePhase, PhaseAssignment>(), 85, "lead=a:b;\nanalyst=c:d;");

        var result = _sut.Validate(output, skills);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("newlines"));
    }

    [Fact]
    public void Validate_WellFormedOutput_ReturnsOk()
    {
        var skills = new[]
        {
            SkillIndex("architect", roles: new[] { SkillRole.Lead }, positiveKeys: new[] { "auth-port" })
        };
        var output = new TriageOutput(
            new Dictionary<PipelinePhase, PhaseAssignment>
            {
                [PipelinePhase.Plan] = new("architect", Array.Empty<string>(), Array.Empty<string>(), null)
            },
            85,
            "lead=architect:auth-port;");

        var result = _sut.Validate(output, skills);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    private static SkillIndexEntry SkillIndex(string name, SkillRole[] roles, string[]? positiveKeys = null) =>
        new(name,
            $"Skill {name}",
            roles,
            new ActivationCriteria(
                (positiveKeys ?? Array.Empty<string>()).Select(k => new ActivationKey(k, k)).ToList(),
                Array.Empty<ActivationKey>()),
            Array.Empty<RoleAssignment>(),
            new Dictionary<SkillRole, OutputForm>());

    private static TriageOutput TriageWith(PipelinePhase phase, string lead) =>
        new(new Dictionary<PipelinePhase, PhaseAssignment>
        {
            [phase] = new(lead, Array.Empty<string>(), Array.Empty<string>(), null)
        }, 85, string.Empty);
}
