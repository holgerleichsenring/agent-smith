using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services;
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
        _loader = new YamlSkillLoader(NullLogger<YamlSkillLoader>.Instance);
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
    public void LoadRoleDefinitions_ValidRoles_ReturnsAll()
    {
        var skillsDir = Path.Combine(_tempDir, "skills");
        Directory.CreateDirectory(skillsDir);

        File.WriteAllText(Path.Combine(skillsDir, "architect.yaml"), """
            name: architect
            display_name: Architect
            emoji: "🏗️"
            description: System architecture
            triggers:
              - architecture
              - design
            rules: Focus on clean architecture patterns
            convergence_criteria:
              - Architecture review complete
            """);

        File.WriteAllText(Path.Combine(skillsDir, "tester.yaml"), """
            name: tester
            display_name: Tester
            emoji: "🧪"
            description: Quality assurance
            triggers:
              - test
            rules: Ensure test coverage
            convergence_criteria:
              - Tests defined
            """);

        var roles = _loader.LoadRoleDefinitions(skillsDir);

        roles.Should().HaveCount(2);
        roles.Should().Contain(r => r.Name == "architect");
        roles.Should().Contain(r => r.Name == "tester");

        var architect = roles.First(r => r.Name == "architect");
        architect.DisplayName.Should().Be("Architect");
        architect.Triggers.Should().Contain("architecture");
        architect.Rules.Should().Contain("clean architecture");
    }

    [Fact]
    public void LoadRoleDefinitions_DirectoryNotFound_ReturnsEmpty()
    {
        var roles = _loader.LoadRoleDefinitions(Path.Combine(_tempDir, "nonexistent"));
        roles.Should().BeEmpty();
    }

    [Fact]
    public void LoadRoleDefinitions_InvalidFile_SkipsIt()
    {
        var skillsDir = Path.Combine(_tempDir, "skills");
        Directory.CreateDirectory(skillsDir);

        File.WriteAllText(Path.Combine(skillsDir, "valid.yaml"), """
            name: architect
            display_name: Architect
            description: Architecture
            """);

        File.WriteAllText(Path.Combine(skillsDir, "invalid.yaml"), ": [broken {{{}");

        var roles = _loader.LoadRoleDefinitions(skillsDir);
        roles.Should().HaveCount(1);
        roles[0].Name.Should().Be("architect");
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
}
