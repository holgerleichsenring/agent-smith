using AgentSmith.Infrastructure.Core.Services;
using FluentAssertions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Tests.Services.Skills;

public sealed class SkillMdFrontmatterFieldsTests
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    [Fact]
    public void Yaml_AllNewFields_DeserializesCorrectly()
    {
        const string yaml = """
            name: complete
            description: a description
            role: investigator
            category: auth
            investigator_mode: survey
            survey_scope:
              - src/**/*.cs
              - tests/**/*.cs
            scope_hint: controllers
            block_condition: never
            loop: true
            output_schema: observation
            activates_when: source_available
            """;

        var meta = Deserializer.Deserialize<SkillMdFrontmatter>(yaml);

        meta.Role.Should().Be("investigator");
        meta.Category.Should().Be("auth");
        meta.InvestigatorMode.Should().Be("survey");
        meta.SurveyScope.Should().BeEquivalentTo(new[] { "src/**/*.cs", "tests/**/*.cs" });
        meta.ScopeHint.Should().Be("controllers");
        meta.BlockCondition.Should().Be("never");
        meta.Loop.Should().BeTrue();
        meta.OutputSchema.Should().Be("observation");
    }

    [Fact]
    public void Yaml_NewFieldsOmitted_AllNullable()
    {
        const string yaml = """
            name: minimal
            description: x
            roles_supported: [analyst]
            """;

        var meta = Deserializer.Deserialize<SkillMdFrontmatter>(yaml);

        meta.Role.Should().BeNull();
        meta.Category.Should().BeNull();
        meta.InvestigatorMode.Should().BeNull();
        meta.SurveyScope.Should().BeNull();
        meta.ScopeHint.Should().BeNull();
        meta.BlockCondition.Should().BeNull();
        meta.Loop.Should().BeNull();
        meta.OutputSchema.Should().BeNull();
    }

    [Fact]
    public void Yaml_LoopAsBool_DeserializesAsBoolean()
    {
        const string yamlTrue = """
            name: t
            role: producer
            loop: true
            """;
        const string yamlFalse = """
            name: f
            role: producer
            loop: false
            """;

        Deserializer.Deserialize<SkillMdFrontmatter>(yamlTrue).Loop.Should().BeTrue();
        Deserializer.Deserialize<SkillMdFrontmatter>(yamlFalse).Loop.Should().BeFalse();
    }
}
