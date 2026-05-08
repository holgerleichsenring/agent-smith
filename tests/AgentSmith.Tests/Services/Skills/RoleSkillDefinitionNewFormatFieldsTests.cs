using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services.Skills;

public sealed class RoleSkillDefinitionNewFormatFieldsTests : IDisposable
{
    private readonly string _tempDir;
    private readonly YamlSkillLoader _loader;

    public RoleSkillDefinitionNewFormatFieldsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "agentsmith-rsd-newfmt-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        _loader = new YamlSkillLoader(
            new StubSkillsCatalogPath(),
            new ConceptVocabularyLoader(NullLogger<ConceptVocabularyLoader>.Instance),
            new ConceptVocabularyValidator(NullLogger<ConceptVocabularyValidator>.Instance),
            new SkillIndexBuilder(NullLogger<SkillIndexBuilder>.Instance),
            new ProviderOverrideResolver(new ActiveProviderResolver(new AgentSmithConfig())),
            NullLogger<YamlSkillLoader>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void NewFormat_FieldsCopiedFromFrontmatter()
    {
        WriteSkill("a", """
            ---
            name: a
            description: "desc"
            role: investigator
            category: auth
            investigator_mode: verify_hint
            scope_hint: "edges"
            output_schema: observation
            activates_when: "source_available"
            loop: true
            ---
            Body content.
            """);

        var skill = _loader.LoadRoleDefinitions(Path.Combine(_tempDir, "skills")).Single();

        skill.Role.Should().Be("investigator");
        skill.Category.Should().Be("auth");
        skill.InvestigatorMode.Should().Be("verify_hint");
        skill.ScopeHint.Should().Be("edges");
        skill.OutputSchema.Should().Be("observation");
        skill.ActivatesWhen.Should().Be("source_available");
        skill.Loop.Should().Be(true);
    }

    [Fact]
    public void LegacyFormat_NewFieldsAreNull()
    {
        WriteSkill("legacy", """
            ---
            name: legacy
            description: "x"
            roles_supported: [analyst]
            ---

            ## as_analyst

            Analyst body.
            """);

        var skill = _loader.LoadRoleDefinitions(Path.Combine(_tempDir, "skills")).Single();

        skill.Role.Should().BeNull();
        skill.Category.Should().BeNull();
        skill.InvestigatorMode.Should().BeNull();
        skill.SurveyScope.Should().BeNull();
        skill.ScopeHint.Should().BeNull();
        skill.BlockCondition.Should().BeNull();
        skill.Loop.Should().BeNull();
        skill.OutputSchema.Should().BeNull();
    }

    private void WriteSkill(string name, string content)
    {
        var dir = Path.Combine(_tempDir, "skills", name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "SKILL.md"), content);
    }
}
