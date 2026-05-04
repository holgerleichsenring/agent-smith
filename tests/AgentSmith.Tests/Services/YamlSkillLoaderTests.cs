using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

public sealed class YamlSkillLoaderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly YamlSkillLoader _loader;

    public YamlSkillLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "agentsmith-skill-" + Guid.NewGuid());
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
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void LoadProjectSkills_FileExists_ReturnsConfig()
    {
        var yaml = """
            input:
              type: ticket
              provider: github
            output:
              type: pull-request
              provider: github
            roles:
              architect:
                enabled: true
              tester:
                enabled: false
            discussion:
              max_rounds: 5
            """;
        File.WriteAllText(Path.Combine(_tempDir, "skill.yaml"), yaml);

        var config = _loader.LoadProjectSkills(_tempDir);

        config.Should().NotBeNull();
        config!.Input.Type.Should().Be("ticket");
        config.Input.Provider.Should().Be("github");
        config.Roles.Should().ContainKey("architect");
        config.Roles["architect"].Enabled.Should().BeTrue();
        config.Roles["tester"].Enabled.Should().BeFalse();
        config.Discussion.MaxRounds.Should().Be(5);
    }

    [Fact]
    public void LoadProjectSkills_NoFile_ReturnsNull()
    {
        var config = _loader.LoadProjectSkills(_tempDir);
        config.Should().BeNull();
    }

    [Fact]
    public void LoadProjectSkills_InvalidYaml_ReturnsNull()
    {
        File.WriteAllText(Path.Combine(_tempDir, "skill.yaml"), ": [invalid yaml {{{}");

        var config = _loader.LoadProjectSkills(_tempDir);
        config.Should().BeNull();
    }

    [Fact]
    public void LoadRoleDefinitions_SkillMdFormat_ReturnsAll()
    {
        var skillsDir = Path.Combine(_tempDir, "skills");
        Directory.CreateDirectory(skillsDir);

        WriteAnalystSkill(skillsDir, "architect", "System architecture",
            "Focus on clean architecture patterns. Evaluate component boundaries.");
        File.WriteAllText(Path.Combine(skillsDir, "architect", "agentsmith.md"), """
            # Agent Smith Extensions

            ## convergence_criteria
            - "Architecture review complete"
            - "Patterns are consistent"
            """);

        WriteAnalystSkill(skillsDir, "tester", "Quality assurance", "Ensure test coverage.");

        var roles = _loader.LoadRoleDefinitions(skillsDir);

        roles.Should().HaveCount(2);
        roles.Should().Contain(r => r.Name == "architect");
        roles.Should().Contain(r => r.Name == "tester");

        var architect = roles.First(r => r.Name == "architect");
        architect.Description.Should().Be("System architecture");
        architect.Rules.Should().Contain("clean architecture");
        architect.RolesSupported.Should().NotBeNull().And.ContainSingle();
        architect.RoleBodies.Should().NotBeNull();
        architect.ConvergenceCriteria.Should().HaveCount(2);
        architect.ConvergenceCriteria.Should().Contain("Architecture review complete");
    }

    [Fact]
    public void LoadRoleDefinitions_SkillMdWithSource_ParsesProvenance()
    {
        var skillsDir = Path.Combine(_tempDir, "skills");
        Directory.CreateDirectory(skillsDir);
        WriteAnalystSkill(skillsDir, "architect", "Architecture", "Architecture rules.");

        File.WriteAllText(Path.Combine(skillsDir, "architect", "source.md"), """
            # Skill Source

            origin: https://github.com/example/skills
            version: v1.2.0
            commit: a3f2c1d
            reviewed: 2026-04-08
            reviewed-by: Holger
            """);

        var roles = _loader.LoadRoleDefinitions(skillsDir);

        roles.Should().HaveCount(1);
        var architect = roles[0];
        architect.Source.Should().NotBeNull();
        architect.Source!.Origin.Should().Be("https://github.com/example/skills");
        architect.Source.Version.Should().Be("v1.2.0");
        architect.Source.Commit.Should().Be("a3f2c1d");
        architect.Source.Reviewed.Should().Be(new DateOnly(2026, 4, 8));
        architect.Source.ReviewedBy.Should().Be("Holger");
    }

    [Fact]
    public void LoadRoleDefinitions_SkillMdNoFrontmatter_SkipsIt()
    {
        var skillsDir = Path.Combine(_tempDir, "skills");
        var badDir = Path.Combine(skillsDir, "bad-skill");
        Directory.CreateDirectory(badDir);

        File.WriteAllText(Path.Combine(badDir, "SKILL.md"), """
            # No frontmatter here

            Just a plain markdown file.
            """);

        var roles = _loader.LoadRoleDefinitions(skillsDir);
        roles.Should().BeEmpty();
    }

    [Fact]
    public void LoadRoleDefinitions_SkillMdMissingRolesSupported_RejectsWithMigrationHint()
    {
        var skillsDir = Path.Combine(_tempDir, "skills");
        var badDir = Path.Combine(skillsDir, "legacy-skill");
        Directory.CreateDirectory(badDir);

        File.WriteAllText(Path.Combine(badDir, "SKILL.md"), """
            ---
            name: legacy-skill
            description: "Old skill without roles_supported"
            version: 1.0.0
            ---

            # Legacy

            Body without role sections.
            """);

        var roles = _loader.LoadRoleDefinitions(skillsDir);
        roles.Should().BeEmpty();
    }

    [Fact]
    public void LoadRoleDefinitions_DirectoryNotFound_ReturnsEmpty()
    {
        var roles = _loader.LoadRoleDefinitions(Path.Combine(_tempDir, "nonexistent"));
        roles.Should().BeEmpty();
    }

    [Fact]
    public void LoadRoleDefinitions_SubdirectoryWithoutSkillMd_Ignored()
    {
        var skillsDir = Path.Combine(_tempDir, "skills");
        var emptyDir = Path.Combine(skillsDir, "empty-dir");
        Directory.CreateDirectory(emptyDir);

        File.WriteAllText(Path.Combine(emptyDir, "README.md"), "Not a skill");

        var roles = _loader.LoadRoleDefinitions(skillsDir);
        roles.Should().BeEmpty();
    }

    [Fact]
    public void LoadRoleDefinitions_ConvergenceCriteriaParsedFromAgentSmithMd()
    {
        var skillsDir = Path.Combine(_tempDir, "skills");
        Directory.CreateDirectory(skillsDir);
        WriteAnalystSkill(skillsDir, "checker", "Checks things", "Check all the things.");

        File.WriteAllText(Path.Combine(skillsDir, "checker", "agentsmith.md"), """
            # Agent Smith Extensions

            ## convergence_criteria
            - "All items checked"
            - "No issues remaining"

            ## notes
            This is a test skill.
            """);

        var roles = _loader.LoadRoleDefinitions(skillsDir);

        roles.Should().HaveCount(1);
        roles[0].ConvergenceCriteria.Should().HaveCount(2);
        roles[0].ConvergenceCriteria[0].Should().Be("All items checked");
        roles[0].ConvergenceCriteria[1].Should().Be("No issues remaining");
    }

    [Fact]
    public void GetActiveRoles_FiltersEnabledOnly()
    {
        var allRoles = new List<RoleSkillDefinition>
        {
            new() { Name = "architect", DisplayName = "Architect", Rules = "Base rules" },
            new() { Name = "tester", DisplayName = "Tester", Rules = "Test rules" },
            new() { Name = "devops", DisplayName = "DevOps", Rules = "DevOps rules" }
        };

        var config = new SkillConfig
        {
            Roles = new Dictionary<string, RoleProjectConfig>
            {
                ["architect"] = new() { Enabled = true },
                ["tester"] = new() { Enabled = false },
                ["devops"] = new() { Enabled = true }
            }
        };

        var active = _loader.GetActiveRoles(allRoles, config);

        active.Should().HaveCount(2);
        active.Should().Contain(r => r.Name == "architect");
        active.Should().Contain(r => r.Name == "devops");
        active.Should().NotContain(r => r.Name == "tester");
    }

    [Fact]
    public void GetActiveRoles_MergesExtraRules()
    {
        var allRoles = new List<RoleSkillDefinition>
        {
            new() { Name = "architect", DisplayName = "Architect", Rules = "Base rules" }
        };

        var config = new SkillConfig
        {
            Roles = new Dictionary<string, RoleProjectConfig>
            {
                ["architect"] = new() { Enabled = true, ExtraRules = "Use DDD patterns" }
            }
        };

        var active = _loader.GetActiveRoles(allRoles, config);

        active.Should().HaveCount(1);
        active[0].Rules.Should().Contain("Base rules");
        active[0].Rules.Should().Contain("Project-Specific Rules");
        active[0].Rules.Should().Contain("Use DDD patterns");
    }

    [Fact]
    public void GetActiveRoles_NoExtraRules_KeepsOriginal()
    {
        var allRoles = new List<RoleSkillDefinition>
        {
            new() { Name = "architect", DisplayName = "Architect", Rules = "Base rules" }
        };

        var config = new SkillConfig
        {
            Roles = new Dictionary<string, RoleProjectConfig>
            {
                ["architect"] = new() { Enabled = true }
            }
        };

        var active = _loader.GetActiveRoles(allRoles, config);

        active.Should().HaveCount(1);
        active[0].Rules.Should().Be("Base rules");
    }

    [Fact]
    public void GetActiveRoles_RoleNotInConfig_Excluded()
    {
        var allRoles = new List<RoleSkillDefinition>
        {
            new() { Name = "architect", DisplayName = "Architect", Rules = "Rules" },
            new() { Name = "unknown", DisplayName = "Unknown", Rules = "Rules" }
        };

        var config = new SkillConfig
        {
            Roles = new Dictionary<string, RoleProjectConfig>
            {
                ["architect"] = new() { Enabled = true }
            }
        };

        var active = _loader.GetActiveRoles(allRoles, config);

        active.Should().HaveCount(1);
        active[0].Name.Should().Be("architect");
    }

    /// <summary>
    /// Writes a minimal valid SKILL.md (analyst-only, body section ## as_analyst) into
    /// <paramref name="skillsDir"/>/<paramref name="name"/>/SKILL.md.
    /// </summary>
    private static void WriteAnalystSkill(string skillsDir, string name, string description, string analystBody)
    {
        var dir = Path.Combine(skillsDir, name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "SKILL.md"), $"""
            ---
            name: {name}
            version: 2.0.0
            description: "{description}"

            roles_supported: [analyst]
            ---

            ## as_analyst

            {analystBody}
            """);
    }
}
