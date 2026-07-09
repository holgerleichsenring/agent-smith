using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Validation;
using FluentAssertions;

namespace AgentSmith.Tests.Validation;

/// <summary>
/// p0315b: the phase-spec draft gate — a reply without a yaml block is a
/// plain answer, a schema-valid block passes, and every failure mode names
/// exactly what is wrong (the error drives the master's re-prompt).
/// </summary>
public sealed class SpecDraftValidatorTests
{
    private readonly SpecDraftValidator _sut = new(new PhaseSpecSchemaProvider());

    [Fact]
    public void Validate_NoYamlBlock_IsAbsent() =>
        _sut.Validate("Just a grounded answer, no artifact.")
            .Should().BeOfType<SpecDraftAbsent>();

    [Fact]
    public void Validate_ValidPhaseSpec_IsValid()
    {
        var reply = """
            Draft below.
            ```yaml
            phase: p9999
            goal: "Add a widget"
            steps:
              - id: impl
                action: "Do the thing"
            done:
              - "widget works"
            ```
            """;

        var outcome = _sut.Validate(reply);

        outcome.Should().BeOfType<SpecDraftValid>()
            .Which.Yaml.Should().Contain("phase: p9999");
    }

    [Fact]
    public void Validate_MissingGoal_IsInvalid_NamingTheField()
    {
        var outcome = _sut.Validate("```yaml\nphase: p9999\n```");

        outcome.Should().BeOfType<SpecDraftInvalid>()
            .Which.Error.Should().Contain("goal");
    }

    [Fact]
    public void Validate_BadPhaseIdPattern_IsInvalid() =>
        _sut.Validate("```yaml\nphase: not-a-phase\ngoal: \"g\"\n```")
            .Should().BeOfType<SpecDraftInvalid>();

    [Fact]
    public void Validate_TwoYamlBlocks_IsInvalid() =>
        _sut.Validate("```yaml\nphase: p1\ngoal: \"g\"\n```\n```yaml\nphase: p2\ngoal: \"g\"\n```")
            .Should().BeOfType<SpecDraftInvalid>()
            .Which.Error.Should().Contain("exactly one");

    [Fact]
    public void Validate_MalformedYaml_IsInvalid() =>
        _sut.Validate("```yaml\nphase: [unclosed\n```")
            .Should().BeOfType<SpecDraftInvalid>()
            .Which.Error.Should().Contain("YAML");
}
