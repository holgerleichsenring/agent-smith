using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services.Skills;

public sealed class SkillMdParserNewFormatTests : IDisposable
{
    private readonly string _tempDir;
    private readonly YamlSkillLoader _loader;

    public SkillMdParserNewFormatTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "agentsmith-newfmt-" + Guid.NewGuid());
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
    public void Parse_RoleAndRolesSupportedBothPresent_RejectsSkill()
    {
        WriteSkill("conflict", """
            ---
            name: conflict
            description: "test"
            role: producer
            roles_supported: [lead]
            output_schema: plan
            activates_when: "true"
            ---
            Body.
            """);

        _loader.LoadRoleDefinitions(Path.Combine(_tempDir, "skills")).Should().BeEmpty();
    }

    [Fact]
    public void Parse_OnlyRolePresent_LoadsAsNewFormat()
    {
        WriteSkill("planner", """
            ---
            name: planner
            description: "test"
            role: producer
            output_schema: plan
            activates_when: "true"
            ---
            Verbatim body content.
            """);

        var skill = _loader.LoadRoleDefinitions(Path.Combine(_tempDir, "skills")).Should().ContainSingle().Which;
        skill.Role.Should().Be("producer");
    }

    [Fact]
    public void Parse_OnlyRolesSupportedPresent_RejectedAsLegacy()
    {
        // p0127c: legacy 'roles_supported' shape no longer parses. Skill is dropped at load.
        WriteSkill("legacy", """
            ---
            name: legacy
            description: "test"
            roles_supported: [analyst]
            ---

            ## as_analyst

            Analyst body.
            """);

        _loader.LoadRoleDefinitions(Path.Combine(_tempDir, "skills")).Should().BeEmpty();
    }

    [Fact]
    public void Parse_NeitherKey_RejectsSkill()
    {
        WriteSkill("no-shape", """
            ---
            name: no-shape
            description: "test"
            ---
            Body.
            """);

        _loader.LoadRoleDefinitions(Path.Combine(_tempDir, "skills")).Should().BeEmpty();
    }

    [Fact]
    public void ParseNewFormat_AllFieldsValid_BuildsRoleSkillDefinition()
    {
        WriteSkill("complete", """
            ---
            name: complete
            description: "Cross-cutting investigator"
            role: investigator
            investigator_mode: survey
            survey_scope: ["src/**/*.cs"]
            scope_hint: "controllers"
            output_schema: observation
            activates_when: "source_available"
            loop: false
            ---
            Body content.
            """);

        var skill = _loader.LoadRoleDefinitions(Path.Combine(_tempDir, "skills")).Should().ContainSingle().Which;
        skill.Role.Should().Be("investigator");
        skill.InvestigatorMode.Should().Be("survey");
        skill.SurveyScope.Should().BeEquivalentTo(new[] { "src/**/*.cs" });
        skill.ScopeHint.Should().Be("controllers");
        skill.OutputSchema.Should().Be("observation");
        skill.ActivatesWhen.Should().Be("source_available");
        skill.Loop.Should().Be(false);
    }

    [Fact]
    public void ParseNewFormat_BodyVerbatim_NoSectionSplitting()
    {
        WriteSkill("body-verbatim", """
            ---
            name: body-verbatim
            description: "test"
            role: producer
            output_schema: plan
            activates_when: "true"
            ---

            ## as_lead

            This is NOT a role-section header in the new format.

            ## as_analyst

            Body continues verbatim.
            """);

        var skill = _loader.LoadRoleDefinitions(Path.Combine(_tempDir, "skills")).Should().ContainSingle().Which;
        skill.RoleBodies.Should().BeNull();
        skill.Rules.Should().Contain("This is NOT a role-section header");
        skill.Rules.Should().Contain("Body continues verbatim");
    }

    [Fact]
    public void ParseNewFormat_LegacyCompatFieldsPopulatedFromRole()
    {
        // p0127c: NewFormatSkillBuilder dual-writes the legacy fields from the new
        // taxonomy so existing triage / index consumers keep working during the
        // [Obsolete] window before p0131 removes them. Producer maps to Lead.
        WriteSkill("legacy-null", """
            ---
            name: legacy-null
            description: "test"
            role: producer
            output_schema: plan
            activates_when: "true"
            ---
            Body.
            """);

        var skill = _loader.LoadRoleDefinitions(Path.Combine(_tempDir, "skills")).Should().ContainSingle().Which;
        skill.RolesSupported.Should().BeEquivalentTo([SkillRole.Lead]);
        skill.RoleBodies.Should().BeNull();
    }

    [Fact]
    public void ParseNewFormat_RoleEnumValueInvalid_Rejected()
    {
        WriteSkill("bad-role", """
            ---
            name: bad-role
            description: "test"
            role: superhero
            output_schema: plan
            activates_when: "true"
            ---
            Body.
            """);

        _loader.LoadRoleDefinitions(Path.Combine(_tempDir, "skills")).Should().BeEmpty();
    }

    [Fact]
    public void ParseNewFormat_DescriptionOver200Chars_Rejected()
    {
        var longDesc = new string('x', 201);
        WriteSkill("long-desc", $"""
            ---
            name: long-desc
            description: "{longDesc}"
            role: producer
            output_schema: plan
            activates_when: "true"
            ---
            Body.
            """);

        _loader.LoadRoleDefinitions(Path.Combine(_tempDir, "skills")).Should().BeEmpty();
    }

    [Fact]
    public void ParseNewFormat_OutputSchemaInvalid_Rejected()
    {
        WriteSkill("bad-schema", """
            ---
            name: bad-schema
            description: "test"
            role: producer
            output_schema: bogus
            activates_when: "true"
            ---
            Body.
            """);

        _loader.LoadRoleDefinitions(Path.Combine(_tempDir, "skills")).Should().BeEmpty();
    }

    [Fact]
    public void ParseNewFormat_InvestigatorModeMissingOnInvestigator_Rejected()
    {
        WriteSkill("no-mode", """
            ---
            name: no-mode
            description: "test"
            role: investigator
            output_schema: observation
            activates_when: "true"
            ---
            Body.
            """);

        _loader.LoadRoleDefinitions(Path.Combine(_tempDir, "skills")).Should().BeEmpty();
    }

    [Fact]
    public void ParseNewFormat_SurveyScopeMissingOnSurveyMode_Rejected()
    {
        WriteSkill("no-scope", """
            ---
            name: no-scope
            description: "test"
            role: investigator
            investigator_mode: survey
            output_schema: observation
            activates_when: "true"
            ---
            Body.
            """);

        _loader.LoadRoleDefinitions(Path.Combine(_tempDir, "skills")).Should().BeEmpty();
    }

    [Fact]
    public void ParseNewFormat_CategoryMissingOnVerifyHintMode_Rejected()
    {
        WriteSkill("no-cat", """
            ---
            name: no-cat
            description: "test"
            role: investigator
            investigator_mode: verify_hint
            output_schema: observation
            activates_when: "true"
            ---
            Body.
            """);

        _loader.LoadRoleDefinitions(Path.Combine(_tempDir, "skills")).Should().BeEmpty();
    }

    [Fact]
    public void ParseNewFormat_BlockConditionMissingOnJudge_Rejected()
    {
        WriteSkill("no-block", """
            ---
            name: no-block
            description: "test"
            role: judge
            output_schema: observation
            activates_when: "true"
            ---
            Body.
            """);

        _loader.LoadRoleDefinitions(Path.Combine(_tempDir, "skills")).Should().BeEmpty();
    }

    [Fact]
    public void ParseNewFormat_OutputSchemaBootstrapWithNonProducerRole_Rejected()
    {
        WriteSkill("bootstrap-judge", """
            ---
            name: bootstrap-judge
            description: "test"
            role: judge
            block_condition: "always"
            output_schema: bootstrap
            activates_when: "true"
            ---
            Body.
            """);

        _loader.LoadRoleDefinitions(Path.Combine(_tempDir, "skills")).Should().BeEmpty();
    }

    [Fact]
    public void ParseNewFormat_EmptyBody_Rejected()
    {
        WriteSkill("empty-body", """
            ---
            name: empty-body
            description: "test"
            role: producer
            output_schema: plan
            activates_when: "true"
            ---


            """);

        _loader.LoadRoleDefinitions(Path.Combine(_tempDir, "skills")).Should().BeEmpty();
    }

    [Fact]
    public void ParseNewFormat_ActivatesWhenMissing_Rejected()
    {
        WriteSkill("no-aw", """
            ---
            name: no-aw
            description: "test"
            role: producer
            output_schema: plan
            ---
            Body.
            """);

        _loader.LoadRoleDefinitions(Path.Combine(_tempDir, "skills")).Should().BeEmpty();
    }

    private void WriteSkill(string name, string content)
    {
        var dir = Path.Combine(_tempDir, "skills", name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "SKILL.md"), content);
    }
}
