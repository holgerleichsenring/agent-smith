using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

public sealed class SkillsConfigYamlTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(Path.GetTempPath(),
        $"agentsmith-skills-yaml-{Guid.NewGuid():N}.yml");

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }

    private AgentSmithConfig Load(string yaml)
    {
        File.WriteAllText(_tempFile, yaml);
        return new YamlConfigurationLoader().LoadConfig(_tempFile);
    }

    [Fact]
    public void SkillsBlock_VersionPopulated_FromCamelCaseAndSnakeCase()
    {
        var cfg = Load("""
            projects: {}
            skills:
              source: default
              version: v1.0.0
              cacheDir: /var/lib/agentsmith/skills
            secrets: {}
            """);

        cfg.Skills.Source.Should().Be(SkillsSourceMode.Default);
        cfg.Skills.Version.Should().Be("v1.0.0");
    }

    [Fact]
    public void SkillsBlock_CacheDir_RequiresSnakeCase()
    {
        var cfg = Load("""
            projects: {}
            skills:
              source: default
              version: v1.0.0
              cache_dir: /custom/path
            secrets: {}
            """);

        cfg.Skills.CacheDir.Should().Be("/custom/path");
    }
}
